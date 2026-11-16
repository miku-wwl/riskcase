using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NUnit.Framework;
using RiskCase.Infrastructure.Data;

namespace RiskCase.IntegrationTests;

[NonParallelizable]
public sealed class MigrationTests
{
    [Test]
    public async Task Upgrade_preserves_known_legacy_rationale_marks_unknown_author_and_requires_new_review()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__RiskCaseDb");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Ignore("Real PostgreSQL required.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        var database = "riskcase_upgrade_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        builder.Database = database;
        try
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261116091901_InitialRiskCase");
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO riskcase.customers ("Id","CustomerNumber","DisplayName","Segment","CountryCode","RiskTier","Created","LastModified")
                  VALUES ('11111111-1111-1111-1111-111111111111','LEGACY','Synthetic','Personal','NZ','Standard',now(),now());
                INSERT INTO riskcase.accounts ("Id","CustomerId","MaskedNumber","AccountType","Currency","Status","OpenedOn","Created","LastModified")
                  VALUES ('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','**1111','Everyday','NZD','Open',current_date,now(),now());
                INSERT INTO riskcase.risk_alerts ("Id","ExternalAlertId","SourceSystem","IngestionFingerprint","Category","DetectedAt","Severity","CustomerId","AccountId","Created","LastModified")
                  VALUES ('33333333-3333-3333-3333-333333333333','LEGACY','Legacy','hash','TransactionRisk',now(),'High','11111111-1111-1111-1111-111111111111','22222222-2222-2222-2222-222222222222',now(),now());
                INSERT INTO riskcase.risk_cases ("Id","AlertId","Status","AssignedAnalystId","Version","AssessmentLevel","AssessmentRationale","AssessedAt","Created","LastModified")
                  VALUES ('44444444-4444-4444-4444-444444444444','33333333-3333-3333-3333-333333333333','Reviewing','possibly-reassigned-user',5,'High','Only the last retained rationale is known.',now(),now(),now());
                INSERT INTO riskcase.audit_events ("Id","CaseId","CaseVersion","ActorId","Operation","DetailsJson","OccurredAt")
                  VALUES (gen_random_uuid(),'44444444-4444-4444-4444-444444444444',4,'actual-legacy-actor','AssessmentRecorded','{{}}',now());
                """);
            await migrator.MigrateAsync();
            var c = await db.RiskCases.AsNoTracking().SingleAsync();
            var assessment = await db.RiskAssessments.AsNoTracking().SingleAsync();
            Assert.Multiple(() => {
                Assert.That(c.Status.ToString(), Is.EqualTo("Investigating")); Assert.That(c.Version, Is.EqualTo(6));
                Assert.That(c.CurrentAssessmentId, Is.Null); Assert.That(c.AssessmentLevel, Is.Null);
                Assert.That(assessment.Rationale, Is.EqualTo("Only the last retained rationale is known."));
                Assert.That(assessment.IsLegacy, Is.True); Assert.That(assessment.AssessorId, Is.Null);
                Assert.That(assessment.CaseVersion, Is.EqualTo(4));
            });
            Assert.That(await db.AuditEvents.CountAsync(x => x.Operation == "LegacyReviewInvalidated" && x.CaseVersion == 6), Is.EqualTo(1));
            Assert.That(await db.Customers.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.Database.GetPendingMigrationsAsync(), Is.Empty);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
}
