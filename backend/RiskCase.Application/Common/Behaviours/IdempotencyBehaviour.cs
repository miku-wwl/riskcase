using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;

namespace RiskCase.Application.Common.Behaviours;

public interface IIdempotentRequest
{
    string IdempotencyKey { get; }
}

public sealed class IdempotencyBehaviour<TRequest, TResponse>(IIdempotencyStore store)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotent)
            return next();

        if (!Guid.TryParseExact(idempotent.IdempotencyKey, "D", out _))
            throw new ApplicationFault(400, "idempotency_key_required", "写操作需要 UUID 格式的 Idempotency-Key。");

        return store.ExecuteAsync(idempotent.IdempotencyKey, Fingerprint(request), () => next(), cancellationToken);
    }

    public static string Fingerprint(object request)
    {
        var json = JsonSerializer.SerializeToElement(request, request.GetType());
        var sorted = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in json.EnumerateObject().Where(p => !StringComparer.OrdinalIgnoreCase.Equals(p.Name, nameof(IIdempotentRequest.IdempotencyKey))))
            sorted.Add(property.Name, property.Value.Clone());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.GetType().FullName + ":" + JsonSerializer.Serialize(sorted))));
    }
}
