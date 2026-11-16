using RiskCase.Domain.Common;

namespace RiskCase.Domain.Entities;

public sealed class IdempotencyReceipt : BaseEntity
{
    public string Scope { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Fingerprint { get; private set; } = string.Empty;
    public string ResponseType { get; private set; } = string.Empty;
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyReceipt() { }

    public static IdempotencyReceipt Create(string scope, string key, string fingerprint, string responseType, string responseJson, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Scope = scope,
        Key = key,
        Fingerprint = fingerprint,
        ResponseType = responseType,
        ResponseJson = responseJson,
        CreatedAt = now
    };
}
