using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RiskCase.IntegrationTests;

/// <summary>Only tests install this interceptor. Both statements take their snapshot before either writer proceeds.</summary>
public sealed class CaseReadBarrier : DbCommandInterceptor
{
    private Guid? _caseId;
    private int _arrivals;
    private TaskCompletionSource _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Arrived => _arrived.Task;
    public void Arm(Guid id) { _caseId = id; _arrivals = 0; _arrived = new(TaskCreationOptions.RunContinuationsAsynchronously); _release = new(TaskCreationOptions.RunContinuationsAsynchronously); }
    public void Release() { _caseId = null; _release.TrySetResult(); }
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (_caseId is { } id && command.CommandText.Contains("risk_case_links") && !command.CommandText.Contains("risk_alerts")
            && command.Parameters.Cast<DbParameter>().Any(p => p.Value is Guid value && value == id))
        {
            if (Interlocked.Increment(ref _arrivals) == 2) _arrived.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        return result;
    }
}
