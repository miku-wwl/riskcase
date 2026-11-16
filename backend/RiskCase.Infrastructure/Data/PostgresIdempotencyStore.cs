using System.Text.Json;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RiskCase.Infrastructure.Data;

public sealed class PostgresIdempotencyStore(ApplicationDbContext db, IUser user, TimeProvider clock) : IIdempotencyStore
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
        catch (DbUpdateConcurrencyException)
        {
            throw new ApplicationFault(412, "version_conflict", "档案已在其他窗口更新。请刷新后重新确认操作。");
        }
    }
}

public sealed class PostgresAdvisoryLockProvider(ApplicationDbContext db) : IAdvisoryLockProvider
{
    public async Task AcquireAsync(string scope, CancellationToken cancellationToken) =>
        _ = await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0))", cancellationToken);
}
