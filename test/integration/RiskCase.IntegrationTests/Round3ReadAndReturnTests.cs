using System.Net;
using System.Text.Json.Nodes;
using Npgsql;
using NUnit.Framework;

namespace RiskCase.IntegrationTests;

public sealed partial class HardeningTests
{
    [Test]
    public async Task Snapshot_in_existing_ingestion_transaction_uses_parent_lock_without_nested_transaction()
    {
        var payload=Payload();using var intake=await SendIntake(payload);var c=await Body(intake);
        c=await Change(_supervisor,c,"assignment",new{analystId=_analystId});c=await Change(_analyst,c,"start");
        var id=Guid.Parse(c["id"]!.ToString());var version=c["version"]!.GetValue<int>();
        _factory.ReadProbe.Arm(id);_factory.ReadProbe.ObserveWriteFor=id;
        var reading=SendIntake(payload);Task<HttpResponseMessage>? writing=null;
        try {
            await _factory.ReadProbe.Arrived.WaitAsync(TimeSpan.FromSeconds(10));
            writing=Send(_analyst,c,"notes",new{text=Reason});
            await _factory.ReadProbe.WriteReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        } finally { _factory.ReadProbe.Release();_factory.ReadProbe.ObserveWriteFor=null; }
        using var readResponse=await reading;Assert.That(readResponse.StatusCode,Is.EqualTo(HttpStatusCode.Created));var view=await Body(readResponse);
        using var writeResponse=await writing!;Assert.That(writeResponse.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        Assert.That(view["version"]!.GetValue<int>(),Is.EqualTo(version));Assert.That(view["notes"]!.AsArray(),Is.Empty);
        Assert.That(view["auditHistory"]!.AsArray().Max(x=>x!["version"]!.GetValue<int>()),Is.EqualTo(version));
        Assert.That(await Scalar("SELECT \"Version\" FROM riskcase.risk_cases WHERE \"Id\"=@id",id.ToString()),Is.EqualTo(version+1));
    }
    [TestCase("")]
    [TestCase("     ")]
    [TestCase("Too short")]
    [TestCase("oversized")]
    public async Task Invalid_return_reason_is_a_400_with_no_committed_changes(string reason)
    {
        var c = await Reviewing(); var before = await Facts(c["id"]!.ToString());
        using var response = await Send(_supervisor, c, "resume", new { reason = reason == "oversized" ? new string('x',1501) : reason });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
        Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo(before));
    }

    [Test]
    public async Task Return_reason_is_trimmed_persisted_attributed_replayable_and_requires_reassessment()
    {
        var c = await Reviewing(); var key = Guid.NewGuid().ToString(); var reason = "  " + Reason + "\n  ";
        var returned = await Change(_supervisor, c, "resume", new { reason }, key);
        using var retry = await Send(_supervisor, c, "resume", new { reason }, key);
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Body(retry))["version"]!.ToString(), Is.EqualTo(returned["version"]!.ToString()));
        using var changed = await Send(_supervisor, c, "resume", new { reason = Reason + " changed" }, key);
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using var resubmit = await Send(_analyst, returned, "review"); Assert.That(resubmit.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await using var db = new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await db.OpenAsync();
        await using var audit = new NpgsqlCommand("SELECT \"DetailsJson\"->>'Reason',\"ActorId\",\"CaseVersion\",\"OccurredAt\" FROM riskcase.audit_events WHERE \"CaseId\"=@id AND \"Operation\"='InvestigationResumed'", db);
        audit.Parameters.AddWithValue("id",Guid.Parse(c["id"]!.ToString()));
        await using (var reader = await audit.ExecuteReaderAsync()) {
            Assert.That(await reader.ReadAsync(), Is.True);
            Assert.Multiple(() => { Assert.That(reader.GetString(0), Is.EqualTo(Reason)); Assert.That(reader.GetString(1), Is.EqualTo(_supervisorId)); Assert.That(reader.GetInt32(2), Is.EqualTo(returned["version"]!.GetValue<int>())); Assert.That(reader.GetDateTime(3), Is.LessThanOrEqualTo(DateTime.UtcNow)); });
            Assert.That(await reader.ReadAsync(), Is.False);
        }
        Assert.That(returned["currentAssessmentId"], Is.Null);
        returned = await Change(_analyst, returned, "assessment", new { level = "low", rationale = Reason });
        await Change(_analyst, returned, "review");
    }

    [Test]
    public async Task Return_enforces_authority_version_and_atomic_concurrent_outcome()
    {
        var c = await Reviewing(); var before = await Facts(c["id"]!.ToString());
        using var forbidden = await Send(_senior, c, "resume", new { reason = Reason }); Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        using var auditor = await Send(_auditor, c, "resume", new { reason = Reason }); Assert.That(auditor.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var stale = c.DeepClone().AsObject(); stale["version"] = c["version"]!.GetValue<int>()-1;
        using var conflict = await Send(_supervisor, stale, "resume", new { reason = Reason }); Assert.That(conflict.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo(before));
        _factory.Barrier.Arm(Guid.Parse(c["id"]!.ToString()));
        var attempts = Task.WhenAll(Send(_supervisor, c, "resume", new { reason = Reason + " A" }), Send(_supervisor, c, "resume", new { reason = Reason + " B" }));
        try { await _factory.Barrier.Arrived.WaitAsync(TimeSpan.FromSeconds(12)); } finally { _factory.Barrier.Release(); }
        var responses = await attempts; Assert.That(responses.Select(r=>(int)r.StatusCode).Order(), Is.EqualTo(new[]{200,412})); foreach (var r in responses) r.Dispose();
        Assert.That(await Scalar("SELECT count(*) FROM riskcase.audit_events WHERE \"CaseId\"=@id AND \"Operation\"='InvestigationResumed'",c["id"]!.ToString()), Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Committed_bodyless_return_receipt_recovers_but_cannot_authorize_a_new_return(bool round2Fingerprint)
    {
        var c = await Reviewing(); var key=Guid.NewGuid().ToString();
        var returned = await Change(_supervisor,c,"resume",new {reason=Reason},key);
        var command = new RiskCase.Application.RiskCases.ResumeRiskCaseCommand(key,Guid.Parse(c["id"]!.ToString()),c["version"]!.GetValue<int>());
        var hash = RiskCase.Application.Common.Behaviours.IdempotencyBehaviour<RiskCase.Application.RiskCases.ResumeRiskCaseCommand,RiskCase.Application.RiskCases.RiskCaseView>.Fingerprint(command,includeCommandType:round2Fingerprint,omitReturnReason:true);
        await using var db=new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")); await db.OpenAsync();
        await using var legacy=new NpgsqlCommand("UPDATE riskcase.idempotency_receipts SET \"Fingerprint\"=@hash, \"ResponseJson\"=\"ResponseJson\"-'LegacyAssessors' WHERE \"Key\"=@key AND \"Scope\"=@actor",db);
        legacy.Parameters.AddWithValue("hash",hash);legacy.Parameters.AddWithValue("key",key);legacy.Parameters.AddWithValue("actor",_supervisorId);Assert.That(await legacy.ExecuteNonQueryAsync(),Is.EqualTo(1));
        var before=await Facts(c["id"]!.ToString());
        using var replay=await Send(_supervisor,c,"resume",key:key);Assert.That(replay.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Body(replay))["version"]!.ToString(),Is.EqualTo(returned["version"]!.ToString()));
        using var changed=await Send(_supervisor,c,"resume",new {reason=Reason},key);Assert.That(changed.StatusCode,Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await Facts(c["id"]!.ToString()),Is.EqualTo(before));
        using var fresh=await Send(_supervisor,c,"resume");Assert.That(fresh.StatusCode,Is.EqualTo(HttpStatusCode.PreconditionFailed));
    }

    [Test]
    public async Task Historical_Round2_receipt_cannot_attach_future_audit_to_older_case_state()
    {
        var c=await Investigating(_analyst,_analystId);var key=Guid.NewGuid().ToString();
        var older=await Change(_analyst,c,"notes",new{text=Reason},key);var current=await Change(_analyst,older,"notes",new{text=Reason+" later"});
        await using var db=new NpgsqlConnection(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb"));await db.OpenAsync();
        await using var inject=new NpgsqlCommand("UPDATE riskcase.idempotency_receipts SET \"ResponseJson\"=jsonb_set(\"ResponseJson\",'{AuditHistory}',\"ResponseJson\"->'AuditHistory'||jsonb_build_array(jsonb_build_object('Version',@version,'Operation','NoteAdded','ActorId',@actor,'OccurredAt',now(),'DetailsJson','{}'))) WHERE \"Key\"=@key AND \"Scope\"=@actor",db);
        inject.Parameters.AddWithValue("version",current["version"]!.GetValue<int>());inject.Parameters.AddWithValue("key",key);inject.Parameters.AddWithValue("actor",_analystId);await inject.ExecuteNonQueryAsync();
        using var response=await Send(_analyst,c,"notes",new{text=Reason},key);Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));var replay=await Body(response);
        Assert.That(replay["version"]!.ToString(),Is.EqualTo(older["version"]!.ToString()));Assert.That(replay["auditHistory"]!.AsArray().Max(x=>x!["version"]!.GetValue<int>()),Is.LessThanOrEqualTo(older["version"]!.GetValue<int>()));
        Assert.That(await Scalar("SELECT count(*) FROM riskcase.risk_case_notes WHERE \"CaseId\"=@id",c["id"]!.ToString()),Is.EqualTo(2));
    }

    private async Task<JsonObject> Reviewing()
    {
        var c=await Investigating(_analyst,_analystId);c=await Change(_analyst,c,"assessment",new {level="high",rationale=Reason});return await Change(_analyst,c,"review");
    }

    [TestCase("notes")]
    [TestCase("assessment")]
    [TestCase("decision")]
    public async Task Detail_is_one_snapshot_across_collections_and_audit(string operation)
    {
        var c = await Investigating(_analyst, _analystId);
        c = await Change(_analyst, c, "notes", new { text = Reason });
        c = await Change(_analyst, c, "assessment", new { level = "high", rationale = Reason });
        if (operation == "decision") c = await Change(_analyst, c, "review");
        var version = c["version"]!.GetValue<int>(); var id = Guid.Parse(c["id"]!.ToString());
        _factory.ReadProbe.Arm(id);
        var reading = Get(_analyst, $"/api/cases/{id}");
        JsonObject committed;
        try
        {
            await _factory.ReadProbe.Arrived.WaitAsync(TimeSpan.FromSeconds(10));
            committed = operation switch {
                "assessment" => await Change(_analyst, c, operation, new { level = "low", rationale = Reason + " revised" }),
                "decision" => await Change(_supervisor, c, operation, new { disposition = "monitor", rationale = Reason }),
                _ => await Change(_analyst, c, operation, new { text = Reason + " later" })
            };
        }
        finally { _factory.ReadProbe.Release(); }
        var read = await reading;
        Assert.Multiple(() => {
            Assert.That(read["version"]!.GetValue<int>(), Is.EqualTo(version));
            Assert.That(read["auditHistory"]!.AsArray().Max(a => a!["version"]!.GetValue<int>()), Is.LessThanOrEqualTo(version), "Read mixed an earlier case with later audit.");
            Assert.That(read["notes"]!.AsArray().Count, Is.EqualTo(1));
            Assert.That(read["assessments"]!.AsArray().Count, Is.EqualTo(1));
            Assert.That(read["decisions"]!.AsArray(), Is.Empty);
        });
        Assert.That(await Scalar("SELECT \"Version\" FROM riskcase.risk_cases WHERE \"Id\"=@id", id.ToString()), Is.EqualTo(version + 1));
        Assert.That(committed["version"]!.GetValue<int>(), Is.EqualTo(version + 1));
    }

    [Test]
    public async Task New_return_without_a_reason_is_rejected_without_audit_or_receipt()
    {
        var c = await Investigating(_analyst, _analystId);
        c = await Change(_analyst, c, "assessment", new { level = "high", rationale = Reason });
        c = await Change(_analyst, c, "review"); var before = await Facts(c["id"]!.ToString());
        using var response = await Send(_supervisor, c, "resume");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
        Assert.That(await Facts(c["id"]!.ToString()), Is.EqualTo(before));
    }
}
