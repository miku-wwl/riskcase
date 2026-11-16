namespace RiskCase.Application.Common.Interfaces;

public interface IIdempotencyStore
{
    Task<TResponse> ExecuteAsync<TResponse>(string key, string fingerprint, Func<Task<TResponse>> action, CancellationToken cancellationToken);
}
