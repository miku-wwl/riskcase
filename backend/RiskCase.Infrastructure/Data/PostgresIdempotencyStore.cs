using System.Text.Json;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RiskCase.Infrastructure.Data;

public sealed class PostgresIdempotencyStore(
    ApplicationDbContext db,
    IUser user,
    TimeProvider clock,
    IHostEnvironment environment,
    ILogger<PostgresIdempotencyStore> logger) : IIdempotencyStore
{
    public async Task<TResponse> ExecuteAsync<TResponse>(string key, string fingerprint, Func<Task<TResponse>> action, CancellationToken cancellationToken)
    {
        var scope = user.Id ?? "upstream-ingestion";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope + ":" + key}, 0))", cancellationToken);

        var prior = await db.IdempotencyReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, cancellationToken);
        var responseType = typeof(TResponse).FullName ?? typeof(TResponse).Name;
        if (prior is not null)
        {
            if (!StringComparer.Ordinal.Equals(prior.Fingerprint, fingerprint) || !StringComparer.Ordinal.Equals(prior.ResponseType, responseType))
                throw new ApplicationFault(409, "idempotency_key_reused", "此幂等键已用于不同的写操作。");
            var replay = JsonSerializer.Deserialize<TResponse>(prior.ResponseJson);
            if (replay is null)
                throw new InvalidOperationException("The stored idempotency response could not be deserialized.");
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
            var entries = string.Join(", ", exception.Entries.Select(DescribeConcurrencyEntry));
            logger.LogWarning(exception, "Optimistic concurrency failed while committing entities: {EntityDiagnostics}.", entries);
            var detail = environment.IsEnvironment("Testing")
                ? $"Integration concurrency diagnostics: {entries}."
                : "保存期间检测到并发写入，当前操作没有生效。请刷新档案后重新确认。";
            throw new ApplicationFault(412, "version_conflict", detail);
        }
    }

    private static string DescribeConcurrencyEntry(EntityEntry entry)
    {
        var version = entry.Metadata.FindProperty("Version");
        if (version is null)
            return entry.Metadata.ClrType.Name;

        var property = entry.Property(version.Name);
        return $"{entry.Metadata.ClrType.Name}(originalVersion={property.OriginalValue}, currentVersion={property.CurrentValue})";
    }
}

public sealed class PostgresAdvisoryLockProvider(ApplicationDbContext db) : IAdvisoryLockProvider
{
    public async Task AcquireAsync(string scope, CancellationToken cancellationToken) =>
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", cancellationToken);
}
