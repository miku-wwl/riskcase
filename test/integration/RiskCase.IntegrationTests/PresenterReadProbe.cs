using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RiskCase.IntegrationTests;

// Installed by the test host only. The reader has already acquired its PostgreSQL snapshot.
public sealed class PresenterReadProbe : DbCommandInterceptor
{
    private Guid? _caseId;
    private int _arrivals;
    private TaskCompletionSource _arrived = NewSignal(), _release = NewSignal();
    public Task Arrived => _arrived.Task;
    public bool Capture { get; set; }
    public ConcurrentQueue<(string Sql, Dictionary<string, object?> Parameters, double Milliseconds)> Commands { get; } = new();
    public ConcurrentQueue<(string Sql, Dictionary<string, object?> Parameters)> Started { get; } = new();
    public Guid? ObserveWriteFor { get; set; }
    public TaskCompletionSource WriteReached { get; private set; } = NewSignal();
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Capture) Started.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => (object?)p.Value)));
        if (ObserveWriteFor is { } id && command.CommandText.Contains("UPDATE riskcase.risk_cases")
            && command.Parameters.Cast<DbParameter>().Any(p => p.Value is Guid value && value == id)) WriteReached.TrySetResult();
        return ValueTask.FromResult(result);
    }
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Arm(Guid id) { _caseId = id; _arrivals = 0; _arrived = NewSignal(); _release = NewSignal(); }
    public void Release() { _caseId = null; _release.TrySetResult(); }
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (Capture) Commands.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => (object?)p.Value), eventData.Duration.TotalMilliseconds));
        if (_caseId is { } id && command.CommandText.Contains("risk_alerts") && command.CommandText.Contains("risk_cases")
            && command.Parameters.Cast<DbParameter>().Any(p => p.Value is Guid value && value == id)
            && Interlocked.Increment(ref _arrivals) == 1)
        {
            _arrived.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        return result;
    }
}
