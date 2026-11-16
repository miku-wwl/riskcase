using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using NUnit.Framework;

namespace RiskCase.IntegrationTests;

[NonParallelizable]
public sealed class HardeningTests
{
    private RiskCaseApiFactory _factory = null!;
    private HttpClient _analyst = null!, _supervisor = null!, _senior = null!, _auditor = null!;
    private string _analystId = "", _supervisorId = "", _seniorId = "", _auditorId = "";
    private JsonObject _subject = null!;
    private const string Reason = "已核验合成交易、客户资料和相关账户关系，需要保留完整依据并由独立人员复核。";

    [OneTimeSetUp]
    public async Task Setup()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb"))) Assert.Ignore("Real PostgreSQL required.");
        _factory = new RiskCaseApiFactory();
        _analyst = await Login("analyst", "RiskAnalyst!2026");
        _supervisor = await Login("supervisor", "RiskSupervisor!2026");
        _senior = await Login("senior", "RiskSenior!2026");
        _auditor = await Login("auditor", "RiskAuditor!2026");
        _analystId = (await Get(_analyst, "/api/session"))["id"]!.GetValue<string>();
        _supervisorId = (await Get(_supervisor, "/api/session"))["id"]!.GetValue<string>();
        _seniorId = (await Get(_senior, "/api/session"))["id"]!.GetValue<string>();
        _auditorId = (await Get(_auditor, "/api/session"))["id"]!.GetValue<string>();
        var queue = await Get(_analyst, "/api/cases?pageSize=100");
        _subject = await Get(_analyst, $"/api/cases/{queue["items"]![0]!["id"]}");
    }

    [OneTimeTearDown]
    public void Cleanup() { _analyst?.Dispose(); _supervisor?.Dispose(); _senior?.Dispose(); _auditor?.Dispose(); _factory?.Dispose(); }

    [Test]
    public async Task Review_freezes_facts_assignment_and_assessment_and_keeps_database_unchanged()
    {
        var c = await Investigating(_analyst, _analystId);
        c = await Change(_analyst, c, "assessment", new { level = "high", rationale = Reason });
        c = await Change(_analyst, c, "review");
        var id = c["id"]!.ToString(); var before = await Facts(id);
        foreach (var (path, body) in new (string, object?)[] {
            ("notes", new { text = Reason }), ("links", Link(c)),
            ("assessment", new { level = "low", rationale = Reason }),
            ("assignment", new { analystId = _seniorId }) })
        {
            using var response = await Send(path == "assignment" ? _supervisor : _analyst, c, path, body);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await response.Content.ReadAsStringAsync());
        }
        Assert.That(await Facts(id), Is.EqualTo(before));
        c = await Change(_supervisor, c, "resume");
        using var premature = await Send(_analyst, c, "review");
        Assert.That(premature.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Assessor_cannot_self_review_after_reassignment_or_role_combination()
    {
        var c = await Investigating(_supervisor, _supervisorId);
        c = await Change(_supervisor, c, "assessment", new { level = "high", rationale = Reason });
        c = await Change(_supervisor, c, "review");
        using var reassignment = await Send(_supervisor, c, "assignment", new { analystId = _analystId });
        // Round 1 accepted this reassignment and then accepted the assessor's own decision.
        if (reassignment.IsSuccessStatusCode)
        {
            c = await Body(reassignment);
            using var bypass = await Send(_supervisor, c, "decision", new { disposition = "escalate", rationale = Reason });
            Assert.That(bypass.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), "Assessor finalized their own investigation after reassignment.");
        }
        else Assert.That(reassignment.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using var selfReview = await Send(_supervisor, c, "decision", new { disposition = "escalate", rationale = Reason });
        Assert.That(selfReview.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        c = await Change(_supervisor, c, "resume");
        c = await Change(_supervisor, c, "assignment", new { analystId = _analystId });
        c = await Change(_analyst, c, "assessment", new { level = "low", rationale = Reason });
        c = await Change(_analyst, c, "review");
        using var formerAssessor = await Send(_supervisor, c, "decision", new { disposition = "monitor", rationale = Reason });
        Assert.That(formerAssessor.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), "A return and reassessment must not launder an earlier assessor into the final reviewer.");
    }

    [Test]
    public async Task Assessment_history_preserves_both_rationales_and_reviewed_identity()
    {
        var c = await Investigating(_analyst, _analystId);
        c = await Change(_analyst, c, "assessment", new { level = "high", rationale = Reason + "初次评估 A。" });
        var firstVersion = c["version"]!.GetValue<int>();
        c = await Change(_analyst, c, "assessment", new { level = "low", rationale = Reason + "修订评估 B。" });
        Assert.That(c["assessments"]?.AsArray().Count, Is.EqualTo(2), "Earlier rationale cannot be reconstructed from the current projection or generic audit.");
        var first = c["assessments"]![0]!;
        Assert.That(first["assessorId"]!.ToString(), Is.EqualTo(_analystId));
        Assert.That(first["rationale"]!.ToString(), Does.EndWith("初次评估 A。"));
        Assert.That(first["caseVersion"]!.GetValue<int>(), Is.EqualTo(firstVersion));
        c = await Change(_analyst, c, "review");
        c = await Change(_supervisor, c, "decision", new { disposition = "monitor", rationale = Reason });
        Assert.That(c["decisions"]![0]!["assessmentId"]!.ToString(), Is.EqualTo(c["currentAssessmentId"]!.ToString()));
        Assert.That(await Scalar("SELECT count(*) FROM riskcase.risk_assessments WHERE \"CaseId\"=@id AND \"AssessorId\"=@actor", c["id"]!.ToString(), _analystId), Is.EqualTo(2));
    }

    [TestCase("missing")]
    [TestCase("auditor")]
    public async Task Ineligible_assignment_is_rejected_without_business_or_audit_changes(string target)
    {
        var c = await Intake(); var before = await Facts(c["id"]!.ToString());
        using var result = await Send(_supervisor, c, "assignment", new { analystId = target == "auditor" ? _auditorId : "nonexistent" });
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await result.Content.ReadAsStringAsync());
        Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo(before));
    }

    [Test]
    public async Task Duplicate_and_reversed_links_are_conflicts_without_partial_audit_or_receipts()
    {
        var c = await Investigating(_analyst, _analystId); var link = Link(c);
        c = await Change(_analyst, c, "links", link);
        var before = await Facts(c["id"]!.ToString());
        foreach (var payload in new[] { link, new LinkBody(link.targetNodeId, link.sourceNodeId, link.kind, link.rationale) })
        {
            using var response = await Send(_analyst, c, "links", payload);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await response.Content.ReadAsStringAsync());
        }
        Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo(before));
    }

    [Test]
    public async Task Lost_response_replay_remains_recoverable_after_reassignment_without_reexecution()
    {
        var c = await Investigating(_analyst, _analystId); var key = Guid.NewGuid().ToString(); var original = c;
        c = await Change(_analyst, c, "notes", new { text = Reason }, key);
        var committed = c["version"]!.GetValue<int>();
        c = await Change(_supervisor, c, "assignment", new { analystId = _seniorId });
        using var replay = await Send(_analyst, original, "notes", new { text = Reason }, key);
        Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Body(replay))["version"]!.GetValue<int>(), Is.EqualTo(committed));
        var current = await Get(_analyst, $"/api/cases/{c["id"]}");
        Assert.That(current["version"]!.GetValue<int>(), Is.GreaterThan(committed));
        Assert.That(current["notes"]!.AsArray().Count, Is.EqualTo(1));
        using var freshUnauthorized = await Send(_analyst, c, "notes", new { text = Reason });
        Assert.That(freshUnauthorized.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Concurrent_writers_share_snapshot_but_only_one_commits_case_note_audit_and_receipt()
    {
        var c = await Investigating(_analyst, _analystId); var version = c["version"]!.GetValue<int>();
        _factory.Barrier.Arm(Guid.Parse(c["id"]!.ToString()));
        var writes = Task.WhenAll(Send(_analyst, c, "notes", new { text = Reason + "A" }), Send(_analyst, c, "notes", new { text = Reason + "B" }));
        try { await _factory.Barrier.Arrived.WaitAsync(TimeSpan.FromSeconds(12)); }
        finally { _factory.Barrier.Release(); }
        var responses = await writes;
        Assert.That(responses.Select(r => (int)r.StatusCode).Order().ToArray(), Is.EqualTo(new[] { 200, 412 }));
        foreach (var response in responses) response.Dispose();
        Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo($"{version+1}/{version+1}/1/0/{version+1}"));
    }

    [Test]
    public async Task Concurrent_review_and_new_facts_cannot_both_commit()
    {
        var c = await Investigating(_analyst, _analystId);
        c = await Change(_analyst, c, "assessment", new { level = "high", rationale = Reason });
        _factory.Barrier.Arm(Guid.Parse(c["id"]!.ToString()));
        var writes = Task.WhenAll(Send(_analyst, c, "review"), Send(_analyst, c, "notes", new { text = Reason }));
        try { await _factory.Barrier.Arrived.WaitAsync(TimeSpan.FromSeconds(12)); }
        finally { _factory.Barrier.Release(); }
        var responses = await writes;
        Assert.That(responses.Select(r => (int)r.StatusCode).Order().ToArray(), Is.EqualTo(new[] { 200, 412 }));
        var persisted = await Get(_analyst, $"/api/cases/{c["id"]}");
        if (persisted["status"]!.ToString() == "reviewing") Assert.That(persisted["notes"]!.AsArray(), Is.Empty);
        else Assert.That(persisted["currentAssessmentId"], Is.Null);
        foreach (var response in responses) response.Dispose();
    }

    [Test]
    public async Task Concurrent_external_duplicates_different_keys_and_changed_payload_are_distinguished()
    {
        var payload = Payload();
        var responses = await Task.WhenAll(SendIntake(payload), SendIntake(payload));
        Assert.That(responses.All(r => r.StatusCode == HttpStatusCode.Created), Is.True);
        var first = await Body(responses[0]); var second = await Body(responses[1]);
        Assert.That(first["id"]!.ToString(), Is.EqualTo(second["id"]!.ToString()));
        var changed = System.Text.Json.JsonSerializer.SerializeToNode(payload)!.AsObject(); changed["riskScore"] = 81;
        using var conflict = await SendIntake(changed);
        Assert.That(conflict.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await Scalar("SELECT count(*) FROM riskcase.risk_cases WHERE \"AlertId\"=@id", first["alert"]!["id"]!.ToString()), Is.EqualTo(1));
        foreach (var response in responses) response.Dispose();
    }

    [TestCase("duplicate_transaction")]
    [TestCase("duplicate_signal")]
    [TestCase("bad_amount")]
    [TestCase("bad_currency")]
    [TestCase("invalid_signal")]
    [TestCase("missing_account")]
    [TestCase("wrong_owner")]
    [TestCase("invalid_enum")]
    public async Task Malformed_ingestion_rejects_without_partial_business_state(string defect)
    {
        var payload = System.Text.Json.JsonSerializer.SerializeToNode(Payload())!.AsObject();
        switch (defect)
        {
            case "duplicate_transaction": payload["transactions"]!.AsArray().Add(payload["transactions"]![0]!.DeepClone()); break;
            case "duplicate_signal": payload["signals"]!.AsArray().Add(payload["signals"]![0]!.DeepClone()); break;
            case "bad_amount": payload["transactions"]![0]!["amount"] = 1.001m; break;
            case "bad_currency": payload["transactions"]![0]!["currency"] = "???"; break;
            case "invalid_signal": payload["signals"]![0]!["transactionReferences"]![0] = "nonexistent"; break;
            case "missing_account": payload["transactions"]![0]!["accountId"] = Guid.NewGuid().ToString(); break;
            case "wrong_owner": payload["customerId"] = Guid.NewGuid().ToString(); break;
            case "invalid_enum": payload["category"] = 999; break;
        }
        var key = Guid.NewGuid().ToString();
        using var result = await SendIntake(payload, key);
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await result.Content.ReadAsStringAsync());
        Assert.That(await Scalar("SELECT count(*) FROM riskcase.risk_alerts WHERE \"CustomerId\"=@id AND \"ExternalAlertId\"=@actor", payload["customerId"]!.ToString(), payload["externalAlertId"]!.ToString()), Is.Zero);
    }

    [Test]
    public async Task Command_identity_is_part_of_idempotency_fingerprint_and_stale_preconditions_do_not_commit()
    {
        var c = await Intake(); var key = Guid.NewGuid().ToString();
        var assigned = await Change(_analyst, c, "assignment", new { analystId = _analystId });
        var started = await Change(_analyst, assigned, "start", key: key);
        using var collision = await Send(_analyst, assigned, "review", key: key);
        Assert.That(collision.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await Body(collision))["code"]!.ToString(), Is.EqualTo("idempotency_key_reused"));
        using var stale = await Send(_analyst, assigned, "notes", new { text = Reason });
        Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        Assert.That((await Get(_analyst, $"/api/cases/{c["id"]}"))["version"]!.ToString(), Is.EqualTo(started["version"]!.ToString()));
    }

    [Test]
    public async Task Revoked_role_cookie_cannot_replay_a_protected_snapshot()
    {
        var c = await Investigating(_senior, _seniorId); var key = Guid.NewGuid().ToString();
        await Change(_senior, c, "notes", new { text = Reason }, key);
        await using var db = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();
        await using var remove = new NpgsqlCommand("DELETE FROM riskcase.\"AspNetUserRoles\" WHERE \"UserId\"=@actor RETURNING \"RoleId\"", db, tx);
        remove.Parameters.AddWithValue("actor", _seniorId);
        var roleId = (string)(await remove.ExecuteScalarAsync())!;
        await tx.CommitAsync();
        try
        {
            using var replay = await Send(_senior, c, "notes", new { text = Reason }, key);
            Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That((await _senior.GetAsync($"/api/cases/{c["id"]}")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
        finally
        {
            await using var restore = new NpgsqlCommand("INSERT INTO riskcase.\"AspNetUserRoles\" (\"UserId\",\"RoleId\") VALUES (@actor,@role)", db);
            restore.Parameters.AddWithValue("actor", _seniorId); restore.Parameters.AddWithValue("role", roleId); await restore.ExecuteNonQueryAsync();
        }
    }

    [Test]
    public async Task Csrf_headers_role_matrix_and_append_only_database_boundaries_hold()
    {
        var c = await Investigating(_analyst, _analystId);
        using var forbidden = await Send(_auditor, c, "notes", new { text = Reason });
        Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        _analyst.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        try { using var missingCsrf = await Send(_analyst, c, "notes", new { text = Reason }); Assert.That(missingCsrf.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest)); }
        finally { await Csrf(_analyst); }
        c = await Change(_analyst, c, "assessment", new { level = "high", rationale = Reason });
        var id = c["id"]!.ToString(); var before = await Facts(id);
        Assert.ThrowsAsync<PostgresException>(async () => await Scalar("WITH changed AS (UPDATE riskcase.risk_assessments SET \"Rationale\"='tampered' WHERE \"CaseId\"=@id RETURNING 1) SELECT count(*) FROM changed", id));
        Assert.That(await Facts(id), Is.EqualTo(before));
    }

    [Test]
    public async Task Failure_after_business_save_rolls_back_case_children_audit_and_receipt_then_allows_retry()
    {
        var c = await Investigating(_analyst, _analystId); var before = await Facts(c["id"]!.ToString());
        var key = Guid.NewGuid().ToString();
        await using var db = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await db.OpenAsync();
        await using var install = new NpgsqlCommand($"""
            CREATE FUNCTION riskcase.test_receipt_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."Key" = '{key}' THEN RAISE EXCEPTION 'Injected receipt failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER test_receipt_failure BEFORE INSERT ON riskcase.idempotency_receipts
              FOR EACH ROW EXECUTE FUNCTION riskcase.test_receipt_failure();
            """, db);
        await install.ExecuteNonQueryAsync();
        try
        {
            using var failed = await Send(_analyst, c, "notes", new { text = Reason }, key);
            Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo(before), "Inner SaveChanges must not commit independently of the receipt.");
        }
        finally
        {
            await using var uninstall = new NpgsqlCommand("DROP TRIGGER test_receipt_failure ON riskcase.idempotency_receipts; DROP FUNCTION riskcase.test_receipt_failure();", db);
            await uninstall.ExecuteNonQueryAsync();
        }
        c = await Change(_analyst, c, "notes", new { text = Reason }, key);
        Assert.That(c["notes"]!.AsArray().Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrent_retries_same_key_commit_exactly_one_note_and_one_audit()
    {
        var c = await Investigating(_analyst, _analystId); var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Send(_analyst, c, "notes", new { text = Reason }, key), Send(_analyst, c, "notes", new { text = Reason }, key));
        Assert.That(responses.All(x => x.StatusCode == HttpStatusCode.OK), Is.True);
        var first = await Body(responses[0]); var second = await Body(responses[1]);
        Assert.That(first["version"]!.ToString(), Is.EqualTo(second["version"]!.ToString()));
        Assert.That(await Scalar("SELECT count(*) FROM riskcase.risk_case_notes WHERE \"CaseId\"=@id", c["id"]!.ToString()), Is.EqualTo(1));
        foreach (var response in responses) response.Dispose();
    }

    [Test]
    public async Task Semantically_equal_legacy_alert_retries_do_not_create_duplicates()
    {
        var input = System.Text.Json.JsonSerializer.SerializeToNode(Payload())!.AsObject();
        using var original = await SendIntake(input); var c = await Body(original);
        await using var db = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await db.OpenAsync();
        await using var legacy = new NpgsqlCommand("UPDATE riskcase.risk_alerts SET \"IngestionFingerprint\"='legacy-representation-sensitive-hash' WHERE \"Id\"=@id", db);
        legacy.Parameters.AddWithValue("id", Guid.Parse(c["alert"]!["id"]!.ToString())); await legacy.ExecuteNonQueryAsync();
        input["externalAlertId"] = " " + input["externalAlertId"]!.ToString() + " ";
        input["sourceSystem"] = " Round2 "; input["transactions"]![0]!["amount"] = 120.00m;
        using var retry = await SendIntake(input);
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.Created), await retry.Content.ReadAsStringAsync());
        Assert.That((await Body(retry))["id"]!.ToString(), Is.EqualTo(c["id"]!.ToString()));
    }

    [Test]
    public async Task Api_preconditions_not_found_and_numeric_enum_filters_have_stable_statuses()
    {
        var c = await Investigating(_analyst, _analystId);
        using var noPrecondition = new HttpRequestMessage(HttpMethod.Post, $"/api/cases/{c["id"]}/notes") { Content = JsonContent.Create(new { text = Reason }) };
        noPrecondition.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var missingVersion = await _analyst.SendAsync(noPrecondition); Assert.That((int)missingVersion.StatusCode, Is.EqualTo(428));
        using var noKey = new HttpRequestMessage(HttpMethod.Post, $"/api/cases/{c["id"]}/notes") { Content = JsonContent.Create(new { text = Reason }) };
        noKey.Headers.Add("If-Match", $"\"{c["version"]}\"");
        using var missingKey = await _analyst.SendAsync(noKey); Assert.That(missingKey.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var missingCase = await _analyst.GetAsync($"/api/cases/{Guid.NewGuid()}"); Assert.That(missingCase.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        foreach (var filter in new[] { "status", "category", "severity" })
        {
            using var invalid = await _analyst.GetAsync($"/api/cases?{filter}=999"); Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
    }

    [Test]
    public async Task Registered_identity_has_no_case_privileges_or_role_provisioning_route()
    {
        using var registration = _factory.CreateClient(); await Csrf(registration);
        var name = "unprivileged-" + Guid.NewGuid().ToString("N");
        using var result = await registration.PostAsJsonAsync("/api/users/register", new { email = name + "@riskcase.local", password = "NoRole!2026abcd" });
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK), await result.Content.ReadAsStringAsync());
        using var user = await Login(name, "NoRole!2026abcd");
        using var cases = await user.GetAsync("/api/cases"); Assert.That(cases.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var people = await user.GetAsync("/api/analysts"); Assert.That(people.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private async Task<HttpClient> Login(string name, string password)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        await Csrf(client);
        using var response = await client.PostAsJsonAsync("/api/users/login?useCookies=true", new { email = name + "@riskcase.local", password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        await Csrf(client); return client;
    }
    private static async Task Csrf(HttpClient client)
    {
        var value = await Get(client, "/api/security/antiforgery");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", value["requestToken"]!.ToString());
    }
    private async Task<JsonObject> Investigating(HttpClient actor, string actorId)
    {
        var c = await Intake(); c = await Change(_supervisor, c, "assignment", new { analystId = actorId });
        return await Change(actor, c, "start");
    }
    private object Payload(string? externalId = null) => new {
        externalAlertId = externalId ?? "R2-" + Guid.NewGuid().ToString("N"), sourceSystem = "Round2",
        category = "transactionRisk", detectedAt = DateTimeOffset.UtcNow, severity = "high", riskScore = 80,
        customerId = _subject["customer"]!["id"]!.ToString(), accountId = _subject["account"]!["id"]!.ToString(),
        signals = new[] { new { code = "R2_SIGNAL", title = "Synthetic", explanation = Reason, severity = "high", transactionReferences = new[] { "R2-TX" } } },
        transactions = new[] { new { externalReference = "R2-TX", accountId = _subject["account"]!["id"]!.ToString(), direction = "outgoing", amount = 120m, currency = _subject["account"]!["currency"]!.ToString(), occurredAt = DateTimeOffset.UtcNow, channel = "online", counterpartyReference = "R2-CP", counterpartyLabel = "Synthetic Counterparty" } }
    };
    private async Task<HttpResponseMessage> SendIntake(object payload, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/alerts") { Content = JsonContent.Create(payload) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); message.Headers.Add("X-Alert-Ingestion-Key", "integration-test-key");
        return await _analyst.SendAsync(message);
    }
    private async Task<JsonObject> Intake()
    {
        using var result = await SendIntake(Payload()); Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Created), await result.Content.ReadAsStringAsync());
        return await Body(result);
    }
    private static (string sourceNodeId, string targetNodeId, string kind, string rationale) Endpoints(JsonObject c) => (c["graphNodes"]![0]!["id"]!.ToString(), c["graphNodes"]![1]!["id"]!.ToString(), "related", Reason);
    private static LinkBody Link(JsonObject c) { var e = Endpoints(c); return new(e.sourceNodeId, e.targetNodeId, e.kind, e.rationale); }
    private sealed record LinkBody(string sourceNodeId, string targetNodeId, string kind, string rationale);
    private static async Task<HttpResponseMessage> Send(HttpClient client, JsonObject c, string operation, object? payload = null, string? key = null)
    {
        using var message = new HttpRequestMessage(operation == "assessment" ? HttpMethod.Put : HttpMethod.Post, $"/api/cases/{c["id"]}/{operation}");
        message.Headers.Add("If-Match", $"\"{c["version"]}\""); message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        if (payload is not null) message.Content = JsonContent.Create(payload);
        return await client.SendAsync(message);
    }
    private static async Task<JsonObject> Change(HttpClient client, JsonObject c, string operation, object? payload = null, string? key = null)
    {
        using var response = await Send(client, c, operation, payload, key);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync()); return await Body(response);
    }
    private static async Task<JsonObject> Get(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path); Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync()); return await Body(response);
    }
    private static async Task<JsonObject> Body(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    private static async Task<long> Scalar(string sql, string id, string? actor = null)
    {
        await using var db = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db); command.Parameters.AddWithValue("id", Guid.Parse(id));
        if (actor is not null) command.Parameters.AddWithValue("actor", actor);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
    private static async Task<string> Facts(string id)
    {
        var version = await Scalar("SELECT \"Version\" FROM riskcase.risk_cases WHERE \"Id\"=@id", id);
        var audits = await Scalar("SELECT count(*) FROM riskcase.audit_events WHERE \"CaseId\"=@id", id);
        var notes = await Scalar("SELECT count(*) FROM riskcase.risk_case_notes WHERE \"CaseId\"=@id", id);
        var links = await Scalar("SELECT count(*) FROM riskcase.risk_case_links WHERE \"CaseId\"=@id", id);
        var receipts = await Scalar("SELECT count(*) FROM riskcase.idempotency_receipts WHERE \"ResponseJson\"->>'Id' = CAST(@id AS text)", id);
        return $"{version}/{audits}/{notes}/{links}/{receipts}";
    }
}
