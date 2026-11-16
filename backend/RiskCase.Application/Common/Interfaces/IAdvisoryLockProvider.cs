namespace RiskCase.Application.Common.Interfaces;

public interface IAdvisoryLockProvider
{
    Task AcquireAsync(string scope, CancellationToken cancellationToken);
}
