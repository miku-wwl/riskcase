using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace RiskCase.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class RiskCaseApiWorkflowTests
{
    private RiskCaseApiFactory _factory = null!;
    private HttpClient _analyst = null!;

    [OneTimeSetUp]
    public async Task StartApplication()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb")))
            Assert.Ignore("PostgreSQL integration tests require ConnectionStrings__RiskCaseDb.");

        _factory = new RiskCaseApiFactory();
        _analyst = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await AddAntiforgeryTokenAsync(_analyst);
        await LoginAsync(_analyst, "analyst@riskcase.local", "RiskAnalyst!2026");
    }

    [OneTimeTearDown]
    public void StopApplication()
    {
        _analyst?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task PostgreSql_workflow_persists_idempotent_intake_audit_and_independent_review()
    {
        using var anonymous = _factory.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync("/api/cases");
        Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var anonymousSessionResponse = await anonymous.GetAsync("/api/session");
        Assert.That(anonymousSessionResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        using var queueResponse = await _analyst.GetAsync("/api/cases?page=1&pageSize=25");
        Assert.That(queueResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await queueResponse.Content.ReadAsStringAsync());
        var page = await queueResponse.Content.ReadFromJsonAsync<RiskCaseQueuePage>();
        Assert.That(page, Is.Not.Null);
        Assert.That(page!.TotalItems, Is.GreaterThanOrEqualTo(4));
        Assert.That(page.Items, Has.Some.Matches<RiskCaseQueueItem>(x => x.Status == "resolved"));

        var customerCase = page.Items.First(x => x.Status == "new" && x.AssignedAnalystId is not null);
        var customer = await _analyst.GetFromJsonAsync<RiskCaseView>($"/api/cases/{customerCase.Id}");
        Assert.That(customer, Is.Not.Null);

        var externalId = $"IT-{Guid.NewGuid():N}";
        var ingestionKey = Guid.NewGuid().ToString();
        var ingestBody = new
        {
            externalAlertId = externalId,
            sourceSystem = "Integration Harness",
            category = "transactionRisk",
            detectedAt = DateTimeOffset.UtcNow,
            severity = "medium",
            riskScore = 54,
            customerId = customer!.Customer.Id,
            accountId = customer.Account.Id,
            signals = new[] { new { code = "TEST_SIGNAL", title = "测试交易频率", explanation = "在短时间内出现多笔需人工核实的合成交易记录。", severity = "medium", transactionReferences = new[] { $"{externalId}-TX" } } },
            transactions = new[] { new { externalReference = $"{externalId}-TX", accountId = customer.Account.Id, direction = "outgoing", amount = 125.5m, currency = customer.Account.Currency, occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10), channel = "online", counterpartyReference = "CP-TEST", counterpartyLabel = "Synthetic Counterparty" } }
        };

        using var firstIntake = await SendIntakeAsync(ingestionKey, externalId, ingestBody);
        Assert.That(firstIntake.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await firstIntake.Content.ReadFromJsonAsync<RiskCaseView>();
        using var replayIntake = await SendIntakeAsync(ingestionKey, externalId, ingestBody);
        Assert.That(replayIntake.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var replay = await replayIntake.Content.ReadFromJsonAsync<RiskCaseView>();
        Assert.That(replay!.Id, Is.EqualTo(created!.Id));

        var startResponse = await MutateAsync(_analyst, $"/api/cases/{customerCase.Id}/start", HttpMethod.Post, customerCase.Version, Guid.NewGuid().ToString(), null);
        Assert.That(startResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await startResponse.Content.ReadAsStringAsync());
        var started = await startResponse.Content.ReadFromJsonAsync<RiskCaseView>();
        Assert.That(started!.Status, Is.EqualTo("investigating"));
        var persistedStart = await _analyst.GetFromJsonAsync<RiskCaseView>($"/api/cases/{customerCase.Id}");
        Assert.That(persistedStart!.Version, Is.EqualTo(started.Version));

        var noteKey = Guid.NewGuid().ToString();
        var noteBody = new { text = "核对历史交易后，决定把新交易对手与另一客户账户的关联交由第二位分析员复核。" };
        using var noteResponse = await MutateAsync(_analyst, $"/api/cases/{customerCase.Id}/notes", HttpMethod.Post, started.Version, noteKey, noteBody);
        Assert.That(noteResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Started version {started.Version}, reloaded version {persistedStart.Version}; response: {await noteResponse.Content.ReadAsStringAsync()}");
        var noted = await noteResponse.Content.ReadFromJsonAsync<RiskCaseView>();
        using var noteReplay = await MutateAsync(_analyst, $"/api/cases/{customerCase.Id}/notes", HttpMethod.Post, started.Version, noteKey, noteBody);
        Assert.That(noteReplay.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var replayedNote = await noteReplay.Content.ReadFromJsonAsync<RiskCaseView>();
        Assert.That(replayedNote!.Notes.Count(x => x.Text == noteBody.text), Is.EqualTo(1));
        Assert.That(replayedNote.Version, Is.EqualTo(noted!.Version));

        var concurrentWrites = await Task.WhenAll(
            MutateAsync(_analyst, $"/api/cases/{customerCase.Id}/notes", HttpMethod.Post, noted.Version, Guid.NewGuid().ToString(), new { text = "并发写入 A：核实交易关联的当前事实。" }),
            MutateAsync(_analyst, $"/api/cases/{customerCase.Id}/notes", HttpMethod.Post, noted.Version, Guid.NewGuid().ToString(), new { text = "并发写入 B：补充另一位分析员记录的事实。" }));
        using var concurrentOne = concurrentWrites[0];
        using var concurrentTwo = concurrentWrites[1];
        Assert.That(concurrentWrites.Count(x => x.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        var concurrentStatuses = string.Join(", ", concurrentWrites.Select(x => $"{(int)x.StatusCode} {x.StatusCode}"));
        Assert.That(concurrentWrites.Count(x => x.StatusCode == HttpStatusCode.PreconditionFailed), Is.EqualTo(1), $"Concurrent response statuses: {concurrentStatuses}");

        using var staleWrite = await MutateAsync(_analyst, $"/api/cases/{customerCase.Id}/assessment", HttpMethod.Put, started.Version, Guid.NewGuid().ToString(), new { level = "high", rationale = "新收款人、交易频率上升和跨账户共享对手方同时出现，需要独立人员复核。" });
        Assert.That(staleWrite.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));

        var latest = await _analyst.GetFromJsonAsync<RiskCaseView>($"/api/cases/{customerCase.Id}");
        var assessed = await PostCaseAsync("assessment", customerCase.Id, latest!.Version, new { level = "high", rationale = "新收款人、交易频率上升和跨账户共享对手方同时出现，需要独立人员复核。" }, HttpMethod.Put);
        var review = await PostCaseAsync("review", customerCase.Id, assessed.Version, null, HttpMethod.Post);

        using var supervisor = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await AddAntiforgeryTokenAsync(supervisor);
        await LoginAsync(supervisor, "supervisor@riskcase.local", "RiskSupervisor!2026");
        using var selfReview = await MutateAsync(supervisor, $"/api/cases/{customerCase.Id}/decision", HttpMethod.Post, review.Version, Guid.NewGuid().ToString(), new { disposition = "escalate", rationale = "独立复核确认该档案需要更高级别的欺诈调查并保留完整的事实记录。" });
        Assert.That(selfReview.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var resolved = await selfReview.Content.ReadFromJsonAsync<RiskCaseView>();
        Assert.That(resolved!.Status, Is.EqualTo("resolved"));
        Assert.That(resolved.Decisions.Single().ReviewerId, Is.Not.EqualTo(customerCase.AssignedAnalystId));
        Assert.That(resolved.AuditHistory.Select(x => x.Operation), Does.Contain("DispositionRecorded"));

        using var auditor = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await AddAntiforgeryTokenAsync(auditor);
        await LoginAsync(auditor, "auditor@riskcase.local", "RiskAuditor!2026");
        Assert.That((await auditor.GetAsync($"/api/cases/{customerCase.Id}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var auditorWrite = await MutateAsync(auditor, $"/api/cases/{customerCase.Id}/notes", HttpMethod.Post, resolved.Version, Guid.NewGuid().ToString(), new { text = "审计用户不得修改业务调查记录。" });
        Assert.That(auditorWrite.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    private async Task<RiskCaseView> PostCaseAsync(string suffix, Guid id, int version, object? body, HttpMethod method)
    {
        using var response = await MutateAsync(_analyst, $"/api/cases/{id}/{suffix}", method, version, Guid.NewGuid().ToString(), body);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RiskCaseView>())!;
    }

    private async Task<HttpResponseMessage> SendIntakeAsync(string idempotencyKey, string externalId, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingestion/alerts") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Alert-Ingestion-Key", "integration-test-key");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _analyst.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> MutateAsync(HttpClient client, string path, HttpMethod method, int version, string key, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        request.Headers.Add("Idempotency-Key", key);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/users/login?useCookies=true", new { email, password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        await AddAntiforgeryTokenAsync(client);
    }

    private static async Task AddAntiforgeryTokenAsync(HttpClient client)
    {
        var issued = await client.GetFromJsonAsync<AntiforgeryTokenView>("/api/security/antiforgery");
        Assert.That(issued?.RequestToken, Is.Not.Null.And.Not.Empty);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", issued!.RequestToken);
    }

    private sealed record AntiforgeryTokenView(string RequestToken);
    private sealed record RiskCaseQueuePage(IReadOnlyList<RiskCaseQueueItem> Items, int Page, int PageSize, int TotalItems, int TotalPages);
    private sealed record RiskCaseQueueItem(Guid Id, int Version, string Status, string? AssignedAnalystId, object Alert);
    private sealed record RiskCaseView(Guid Id, int Version, string Status, CustomerView Customer, AccountView Account, IReadOnlyList<NoteView> Notes, IReadOnlyList<DecisionView> Decisions, IReadOnlyList<AuditView> AuditHistory);
    private sealed record CustomerView(Guid Id);
    private sealed record AccountView(Guid Id, string Currency);
    private sealed record NoteView(Guid Id, string Text);
    private sealed record DecisionView(Guid Id, string ReviewerId);
    private sealed record AuditView(string Operation);
}
