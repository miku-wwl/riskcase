using System.Text.Json;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Entities;
using RiskCase.Application.RiskCases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace RiskCase.Infrastructure.Data;

public sealed class PostgresIdempotencyStore(
    ApplicationDbContext db,
    IUser user,
    TimeProvider clock,
    ILogger<PostgresIdempotencyStore> logger) : IIdempotencyStore
{
    public async Task<TResponse> ExecuteAsync<TResponse>(string key, string fingerprint, Func<Task<TResponse>> action, CancellationToken cancellationToken,
        Func<string, string, bool>? legacyReplayProof = null)
    {
        var scope = user.Id ?? "upstream-ingestion";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope + ":" + key}, 0))", cancellationToken);

        var prior = await db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, cancellationToken);
        var responseType = typeof(TResponse).FullName ?? typeof(TResponse).Name;
        if (prior is not null)
        {
            if (!StringComparer.Ordinal.Equals(prior.ResponseType, responseType)
                || !StringComparer.Ordinal.Equals(prior.Fingerprint, fingerprint) && !(legacyReplayProof?.Invoke(prior.Fingerprint, prior.ResponseJson) ?? false))
                throw new ApplicationFault(409, "idempotency_key_reused", "此幂等键已用于不同的写操作。");
            var replay = JsonSerializer.Deserialize<TResponse>(prior.ResponseJson);
            if (replay is null)
                throw new InvalidOperationException("The stored idempotency response could not be deserialized.");
            if (replay is RiskCaseView legacy)
                replay = (TResponse)(object)(legacy with {
                    Assessments = legacy.Assessments ?? [], LegacyAssessors = legacy.LegacyAssessors ?? [],
                    // R2 receipts may predate the consistent presenter. Future audit events
                    // cannot be part of this historical outcome's defined case version.
                    AuditHistory = legacy.AuditHistory.Where(x => x.Version <= legacy.Version).ToArray()
                });
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        try
        {
            var response = await action();
            db.IdempotencyReceipts.Add(IdempotencyReceipt.Create(scope, key, fingerprint, responseType, JsonSerializer.Serialize(response), clock.GetUtcNow()));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entities = string.Join(", ", exception.Entries.Select(entry => entry.Metadata.ClrType.Name));
            logger.LogWarning(exception, "Optimistic concurrency failed while committing entities: {EntityTypes}.", entities);
            throw new ApplicationFault(412, "version_conflict", "保存期间检测到并发写入，当前操作没有生效。请刷新档案后重新确认。");
        }
        catch (DbUpdateException exception) when (IsVersionConstraintConflict(exception))
        {
            logger.LogWarning(exception, "A competing write occupied the same case audit or decision version.");
            throw new ApplicationFault(412, "version_conflict", "保存期间检测到并发写入，当前操作没有生效。请刷新档案后重新确认。");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_risk_case_links_CaseId_SourceNodeId_TargetNodeId" })
        {
            throw new ApplicationFault(409, "duplicate_case_link", "该实体对已有关联记录。");
        }
        catch (Exception exception) when ((exception as PostgresException ?? exception.InnerException as PostgresException)
            is { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure })
        {
            throw new ApplicationFault(503, "transaction_retry_required", "数据库已回滚竞争事务，请沿用原幂等键重试。");
        }
    }

    private static bool IsVersionConstraintConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_audit_events_CaseId_CaseVersion" or "IX_risk_decisions_CaseId_CaseVersion" or "IX_risk_assessments_CaseId_CaseVersion"
        };
}

public sealed class PostgresAdvisoryLockProvider(ApplicationDbContext db) : IAdvisoryLockProvider
{
    public async Task AcquireAsync(string scope, CancellationToken cancellationToken) =>
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", cancellationToken);
}
