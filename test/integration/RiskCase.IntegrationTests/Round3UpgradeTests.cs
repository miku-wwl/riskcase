using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using RiskCase.Infrastructure.Data;
using RiskCase.Infrastructure.Identity;

namespace RiskCase.IntegrationTests;

[NonParallelizable]
public sealed class Round3UpgradeTests
{
    private const string Initial = "20261116091901_InitialRiskCase";
    private const string Round2 = "20261116141547_AssessmentReviewIntegrity";
    private const string Reason = "The beneficiary ownership and recurring outbound transfers require further investigation.";
    private static readonly Guid CaseId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Test]
    public async Task Fresh_database_initializes_all_three_migrations_without_legacy_facts()
    {
        await InDatabase(async connection=>{
            using var factory=new RiskCaseApiFactory{ConnectionString=connection};using var client=await Login(factory,"analyst","RiskAnalyst!2026");
            await using var db=Context(connection);Assert.That((await db.Database.GetAppliedMigrationsAsync()).Count(),Is.EqualTo(3));
            Assert.That(await db.Database.GetPendingMigrationsAsync(),Is.Empty);Assert.That(await db.RiskCases.CountAsync(),Is.GreaterThan(0));Assert.That(await db.LegacyAssessorParticipation.CountAsync(),Is.Zero);
        });
    }

    [Test]
    public async Task Round2_bodyless_return_receipt_and_reasonless_audit_survive_the_actual_schema_upgrade()
    {
        await InDatabase(async connection=>{
            var key=Guid.NewGuid().ToString();string receiptBefore;
            await using(var db=Context(connection)) {
                await db.GetService<IMigrator>().MigrateAsync(Initial);await SeedLegacy(db);
                var email="supervisor@riskcase.local";var user=new ApplicationUser{Id="old-author-1",UserName=email,NormalizedUserName=email.ToUpperInvariant(),Email=email,NormalizedEmail=email.ToUpperInvariant(),EmailConfirmed=true,SecurityStamp=Guid.NewGuid().ToString()};
                user.PasswordHash=new PasswordHasher<ApplicationUser>().HashPassword(user,"RiskSupervisor!2026");db.Users.Add(user);await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("""
                    UPDATE riskcase.risk_cases SET "Status"='Investigating',"Version"=8;
                    INSERT INTO riskcase.audit_events ("Id","CaseId","CaseVersion","ActorId","Operation","DetailsJson","OccurredAt")
                      VALUES (gen_random_uuid(),'44444444-4444-4444-4444-444444444444',8,'old-author-1','InvestigationResumed','{{}}',now());
                    """);
                await db.GetService<IMigrator>().MigrateAsync(Round2);
                var c=await db.RiskCases.AsNoTracking().Include(x=>x.Alert).ThenInclude(x=>x.Customer).Include(x=>x.Alert).ThenInclude(x=>x.Account).Include(x=>x.Assessments).SingleAsync();
                var audit=await db.AuditEvents.OrderBy(x=>x.CaseVersion).Select(x=>new RiskCase.Application.RiskCases.AuditView(x.CaseVersion,x.Operation,x.ActorId,x.OccurredAt,x.DetailsJson)).ToListAsync();
                var response=RiskCase.Application.RiskCases.RiskCasePresenter.Map(c,audit);
                var json=System.Text.Json.JsonSerializer.SerializeToNode(response)!.AsObject();json.Remove("LegacyAssessors");receiptBefore=json.ToJsonString();
                var command=new RiskCase.Application.RiskCases.ResumeRiskCaseCommand(key,CaseId,7);
                var hash=RiskCase.Application.Common.Behaviours.IdempotencyBehaviour<RiskCase.Application.RiskCases.ResumeRiskCaseCommand,RiskCase.Application.RiskCases.RiskCaseView>.Fingerprint(command,omitReturnReason:true);
                db.IdempotencyReceipts.Add(RiskCase.Domain.Entities.IdempotencyReceipt.Create("old-author-1",key,hash,typeof(RiskCase.Application.RiskCases.RiskCaseView).FullName!,receiptBefore,DateTimeOffset.UtcNow));await db.SaveChangesAsync();
                await db.GetService<IMigrator>().MigrateAsync();
            }
            using var factory=new RiskCaseApiFactory{ConnectionString=connection};using var supervisor=await Login(factory,"supervisor","RiskSupervisor!2026");
            var original=new JsonObject{["id"]=CaseId.ToString(),["version"]=7};using var replay=await Send(supervisor,original,"resume",key:key);Assert.That(replay.StatusCode,Is.EqualTo(HttpStatusCode.OK));
            var historical=JsonNode.Parse(await replay.Content.ReadAsStringAsync())!;Assert.That(historical["version"]!.GetValue<int>(),Is.EqualTo(8));Assert.That(historical["legacyAssessors"]!.AsArray(),Is.Empty);
            await using var sql=new NpgsqlConnection(connection);await sql.OpenAsync();
            await using var verify=new NpgsqlCommand("SELECT (SELECT \"DetailsJson\"::text FROM riskcase.audit_events WHERE \"Operation\"='InvestigationResumed'),(SELECT count(*) FROM riskcase.audit_events),(SELECT \"ResponseJson\"::text FROM riskcase.idempotency_receipts WHERE \"Key\"=@key)",sql);verify.Parameters.AddWithValue("key",key);
            await using var reader=await verify.ExecuteReaderAsync();await reader.ReadAsync();Assert.That(reader.GetString(0),Is.EqualTo("{}"));Assert.That(reader.GetInt64(1),Is.EqualTo(3));
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(reader.GetString(2)),JsonNode.Parse(receiptBefore)),Is.True);
        });
    }

    [TestCase("Round1", 1)]
    [TestCase("Round1", 2)]
    [TestCase("Round2", 1)]
    [TestCase("Round2", 2)]
    public async Task Upgrade_recovers_all_old_authors_and_rejects_finalization(string startingPoint, int authors)
    {
        await InDatabase(async connection => {
            await using (var db = Context(connection))
            {
                await db.GetService<IMigrator>().MigrateAsync(Initial);
                foreach (var (name, password, id) in new[] {
                    ("supervisor", "RiskSupervisor!2026", "old-author-1"),
                    ("senior", "RiskSenior!2026", "old-author-2") })
                {
                    var email = name + "@riskcase.local";
                    var user = new ApplicationUser { Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email,
                        NormalizedEmail = email.ToUpperInvariant(), EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString() };
                    user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);
                    db.Users.Add(user);
                }
                await db.SaveChangesAsync();
                await SeedLegacy(db);
                if (authors == 1) await db.Database.ExecuteSqlRawAsync("DELETE FROM riskcase.audit_events WHERE \"CaseVersion\"=6");
                if (startingPoint == "Round2") await db.GetService<IMigrator>().MigrateAsync(Round2);
            }
            using var factory = new RiskCaseApiFactory { ConnectionString = connection };
            using var supervisor = await Login(factory, "supervisor", "RiskSupervisor!2026");
            using var analyst = await Login(factory, "analyst", "RiskAnalyst!2026");
            await using (var scope = factory.Services.CreateAsyncScope()) {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                Assert.That((await users.AddToRoleAsync((await users.FindByEmailAsync("senior@riskcase.local"))!, "RiskSupervisor")).Succeeded, Is.True);
                var eligibleUser = new ApplicationUser { UserName="independent@riskcase.local",Email="independent@riskcase.local",EmailConfirmed=true };
                Assert.That((await users.CreateAsync(eligibleUser,"Independent!2026")).Succeeded, Is.True);
                Assert.That((await users.AddToRoleAsync(eligibleUser,"RiskSupervisor")).Succeeded, Is.True);
            }
            using var senior = await Login(factory, "senior", "RiskSenior!2026");
            using var other = await Login(factory,"independent","Independent!2026");
            var analystId = (await Get(analyst, "/api/session"))["id"]!.ToString();
            var c = await Get(analyst, $"/api/cases/{CaseId}");
            Assert.That(c["version"]!.GetValue<int>(), Is.EqualTo(8));
            c = await Change(supervisor, c, "assignment", new { analystId });
            c = await Change(analyst, c, "assessment", new { level = "low", rationale = Reason });
            var prior=c;factory.ReadProbe.Arm(CaseId);var reading=Get(analyst,$"/api/cases/{CaseId}");
            try { await factory.ReadProbe.Arrived.WaitAsync(TimeSpan.FromSeconds(10));c=await Change(analyst,c,"assessment",new{level="high",rationale=Reason+" New evidence."}); }
            finally { factory.ReadProbe.Release(); }
            var historical=await reading;Assert.That(historical["version"]!.ToString(),Is.EqualTo(prior["version"]!.ToString()));
            Assert.That(historical["assessments"]!.AsArray().Count,Is.EqualTo(2));Assert.That(historical["auditHistory"]!.AsArray().Max(x=>x!["version"]!.GetValue<int>()),Is.LessThanOrEqualTo(prior["version"]!.GetValue<int>()));
            c = await Change(analyst, c, "review");
            using var result = await Send(supervisor, c, "decision", new { disposition = "monitor", rationale = Reason });
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), "Historical author finalized after reassignment and reassessment: " + await result.Content.ReadAsStringAsync());
            await using var independent = new NpgsqlConnection(connection); await independent.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT \"Status\" || '/' || \"Version\" FROM riskcase.risk_cases WHERE \"Id\"=@id", independent);
            command.Parameters.AddWithValue("id", CaseId);
            Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo($"Reviewing/{c["version"]}"));
            await using (var provenance = new NpgsqlCommand("SELECT count(*),count(DISTINCT \"AssessorId\") FROM riskcase.legacy_assessor_participation",independent))
            await using (var reader=await provenance.ExecuteReaderAsync()) { await reader.ReadAsync(); Assert.That(reader.GetInt64(0),Is.EqualTo(authors)); Assert.That(reader.GetInt64(1),Is.EqualTo(authors)); }
            Assert.That(c["assessments"]!.AsArray().Count,Is.EqualTo(3),"Only the retained legacy snapshot and two actual new assessments may exist.");
            if(authors==2) { using var second=await Send(senior,c,"decision",new{disposition="monitor",rationale=Reason}); Assert.That(second.StatusCode,Is.EqualTo(HttpStatusCode.Conflict)); }
            c=await Change(supervisor,c,"resume",new{reason=Reason});
            c=await Change(supervisor,c,"assignment",new{analystId="old-author-2"});
            c=await Change(senior,c,"assessment",new{level="high",rationale=Reason});c=await Change(senior,c,"review");
            using var stillForbidden=await Send(supervisor,c,"decision",new{disposition="monitor",rationale=Reason});Assert.That(stillForbidden.StatusCode,Is.EqualTo(HttpStatusCode.Conflict));
            var resolved=await Change(other,c,"decision",new{disposition="monitor",rationale=Reason});Assert.That(resolved["status"]!.ToString(),Is.EqualTo("resolved"));
            await using var upgraded=Context(connection);await upgraded.GetService<IMigrator>().MigrateAsync();await upgraded.GetService<IMigrator>().MigrateAsync();
            Assert.That(await upgraded.LegacyAssessorParticipation.CountAsync(),Is.EqualTo(authors));
            Assert.That(await upgraded.AuditEvents.CountAsync(x=>x.Operation=="AssessmentRecorded" && x.CaseVersion<8),Is.EqualTo(authors));
            Assert.That((await upgraded.RiskAssessments.SingleAsync(x=>x.IsLegacy)).Rationale,Is.EqualTo("Latest retained rationale only; older explanations are unavailable."));
            Assert.That(await upgraded.Database.GetPendingMigrationsAsync(),Is.Empty);
        });
    }

    [TestCase("missing-audit")]
    [TestCase("empty-actor")]
    [TestCase("incomplete-details")]
    [TestCase("legacy-decision")]
    public async Task Upgrade_preserves_unknowns_decisions_and_immutable_provenance(string shape)
    {
        await InDatabase(async connection=>{
            await using var db=Context(connection);await db.GetService<IMigrator>().MigrateAsync(Initial);await SeedLegacy(db);
            if(shape=="missing-audit") await db.Database.ExecuteSqlRawAsync("DELETE FROM riskcase.audit_events");
            if(shape=="empty-actor") await db.Database.ExecuteSqlRawAsync("UPDATE riskcase.audit_events SET \"ActorId\"='' WHERE \"CaseVersion\"=6");
            if(shape=="incomplete-details") await db.Database.ExecuteSqlRawAsync("UPDATE riskcase.audit_events SET \"DetailsJson\"='{{}}'::jsonb");
            if(shape=="legacy-decision") await db.Database.ExecuteSqlRawAsync("""
                UPDATE riskcase.risk_cases SET "Status"='Resolved',"Version"=8;
                INSERT INTO riskcase.risk_decisions ("Id","CaseId","Disposition","Rationale","AnalystId","DecidedAt","CaseVersion")
                  VALUES (gen_random_uuid(),'44444444-4444-4444-4444-444444444444','Monitor','Retained historical final decision','old-reviewer',now(),8);
                """);
            await db.GetService<IMigrator>().MigrateAsync(Round2);
            var beforeAudit=await db.AuditEvents.AsNoTracking().OrderBy(x=>x.CaseVersion).Select(x=>x.DetailsJson).ToArrayAsync();
            await db.GetService<IMigrator>().MigrateAsync();await db.GetService<IMigrator>().MigrateAsync();
            Assert.That(await db.AuditEvents.AsNoTracking().OrderBy(x=>x.CaseVersion).Select(x=>x.DetailsJson).ToArrayAsync(),Is.EqualTo(beforeAudit));
            Assert.That(await db.RiskAssessments.CountAsync(),Is.EqualTo(1));
            var history=await db.LegacyAssessorParticipation.AsNoTracking().ToArrayAsync();
            Assert.That(history.Length,Is.EqualTo(shape=="missing-audit" ? 1 : 2));
            Assert.That(history.Count(x=>x.AssessorId==null),Is.EqualTo(shape is "missing-audit" or "empty-actor" ? 1 : 0));
            if(shape=="legacy-decision") {
                var decision=await db.RiskDecisions.SingleAsync();Assert.That(decision.AssessmentId,Is.Null);Assert.That(decision.Rationale,Is.EqualTo("Retained historical final decision"));
                Assert.That((await db.RiskCases.SingleAsync()).Version,Is.EqualTo(8));
            } else {
                var c=await db.RiskCases.Include(x=>x.Assessments).Include(x=>x.LegacyAssessorParticipation).SingleAsync();
                c.Assign("new-investigator",DateTimeOffset.UtcNow);c.RecordAssessment("new-investigator",RiskCase.Domain.Enums.RiskAssessmentLevel.Low,Reason,DateTimeOffset.UtcNow);c.RequestReview("new-investigator",DateTimeOffset.UtcNow);
                if(history.Any(x=>x.AssessorId==null)) Assert.That(Assert.Throws<RiskCase.Domain.Common.DomainRuleException>(()=>c.Decide("independent",RiskCase.Domain.Enums.RiskDisposition.Monitor,Reason,DateTimeOffset.UtcNow))!.Code,Is.EqualTo("legacy_assessor_identity_unavailable"));
                else Assert.DoesNotThrow(()=>c.Decide("independent",RiskCase.Domain.Enums.RiskDisposition.Monitor,Reason,DateTimeOffset.UtcNow));
            }
            if(history.Any(x=>x.AssessorId==null)) {
                using var factory=new RiskCaseApiFactory{ConnectionString=connection};using var supervisor=await Login(factory,"supervisor","RiskSupervisor!2026");using var analyst=await Login(factory,"analyst","RiskAnalyst!2026");
                var actor=(await Get(analyst,"/api/session"))["id"]!.ToString();var active=await Get(analyst,$"/api/cases/{CaseId}");
                active=await Change(supervisor,active,"assignment",new{analystId=actor});active=await Change(analyst,active,"assessment",new{level="low",rationale=Reason});active=await Change(analyst,active,"review");
                using var denied=await Send(supervisor,active,"decision",new{disposition="monitor",rationale=Reason});Assert.That(denied.StatusCode,Is.EqualTo(HttpStatusCode.Conflict));
                Assert.That(JsonNode.Parse(await denied.Content.ReadAsStringAsync())!["code"]!.ToString(),Is.EqualTo("legacy_assessor_identity_unavailable"));
            }
            await using var sql=new NpgsqlConnection(connection);await sql.OpenAsync();
            await using var tamper=new NpgsqlCommand("UPDATE riskcase.legacy_assessor_participation SET \"AssessorId\"='tampered'",sql);
            Assert.That(Assert.ThrowsAsync<PostgresException>(async()=>await tamper.ExecuteNonQueryAsync())!.SqlState,Is.EqualTo("23514"));
            await using var indexes=new NpgsqlCommand("SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid=i.indrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='riskcase' AND NOT i.indisvalid",sql);
            Assert.That(await indexes.ExecuteScalarAsync(),Is.EqualTo(0));
        });
    }

    internal static ApplicationDbContext Context(string connection) => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    internal static async Task InDatabase(Func<string, Task> test)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Ignore("Real PostgreSQL required.");
        var builder = new NpgsqlConnectionStringBuilder(connection); var database = "riskcase_r3_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        builder.Database = database;
        try { await test(builder.ConnectionString); }
        finally { NpgsqlConnection.ClearAllPools(); await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync(); }
    }
    internal static Task SeedLegacy(ApplicationDbContext db) => db.Database.ExecuteSqlRawAsync("""
        INSERT INTO riskcase.customers ("Id","CustomerNumber","DisplayName","Segment","CountryCode","RiskTier","Created","LastModified")
          VALUES ('11111111-1111-1111-1111-111111111111','LEGACY','Synthetic','Personal','NZ','Standard',now(),now());
        INSERT INTO riskcase.accounts ("Id","CustomerId","MaskedNumber","AccountType","Currency","Status","OpenedOn","Created","LastModified")
          VALUES ('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','**1111','Everyday','NZD','Open',current_date,now(),now());
        INSERT INTO riskcase.risk_alerts ("Id","ExternalAlertId","SourceSystem","IngestionFingerprint","Category","DetectedAt","Severity","CustomerId","AccountId","Created","LastModified")
          VALUES ('33333333-3333-3333-3333-333333333333','LEGACY','Legacy','hash','TransactionRisk',now(),'High','11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222',now(),now());
        INSERT INTO riskcase.risk_cases ("Id","AlertId","Status","AssignedAnalystId","Version","AssessmentLevel","AssessmentRationale","AssessedAt","Created","LastModified")
          VALUES ('44444444-4444-4444-4444-444444444444','33333333-3333-3333-3333-333333333333','Reviewing','old-author-2',7,'Low','Latest retained rationale only; older explanations are unavailable.',now(),now(),now());
        INSERT INTO riskcase.audit_events ("Id","CaseId","CaseVersion","ActorId","Operation","DetailsJson","OccurredAt") VALUES
          (gen_random_uuid(),'44444444-4444-4444-4444-444444444444',4,'old-author-1','AssessmentRecorded','{{"Level":"High"}}',now()),
          (gen_random_uuid(),'44444444-4444-4444-4444-444444444444',6,'old-author-2','AssessmentRecorded','{{"Level":"Low"}}',now());
        """);
    internal static async Task<HttpClient> Login(RiskCaseApiFactory factory, string name, string password)
    {
        var client = factory.CreateClient(new() { HandleCookies = true, AllowAutoRedirect = false });
        await Csrf(client);
        using var login = await client.PostAsJsonAsync("/api/users/login?useCookies=true", new { email = name + "@riskcase.local", password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK), await login.Content.ReadAsStringAsync()); await Csrf(client); return client;
    }
    private static async Task Csrf(HttpClient client) { var value = await Get(client, "/api/security/antiforgery"); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", value["requestToken"]!.ToString()); }
    internal static async Task<JsonObject> Get(HttpClient client, string path) { using var response = await client.GetAsync(path); Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK)); return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject(); }
    internal static async Task<HttpResponseMessage> Send(HttpClient client, JsonObject c, string operation, object? payload = null, string? key = null)
    {
        using var request = new HttpRequestMessage(operation == "assessment" ? HttpMethod.Put : HttpMethod.Post, $"/api/cases/{c["id"]}/{operation}");
        request.Headers.Add("If-Match", $"\"{c["version"]}\""); request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        if (payload is not null) request.Content = JsonContent.Create(payload); return await client.SendAsync(request);
    }
    internal static async Task<JsonObject> Change(HttpClient client, JsonObject c, string operation, object? payload = null)
    {
        using var response = await Send(client, c, operation, payload); Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync()); return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }
}
