using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

public sealed class RiskSignal : BaseEntity
{
    public Guid AlertId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Explanation { get; private set; } = string.Empty;
    public RiskSeverity Severity { get; private set; }
    public string[] TransactionReferences { get; private set; } = [];
    public RiskAlert Alert { get; private set; } = null!;

    private RiskSignal() { }

    public static RiskSignal Explain(
        Guid alertId,
        string code,
        string title,
        string explanation,
        RiskSeverity severity,
        IEnumerable<string>? transactionReferences = null)
    {
        if (alertId == Guid.Empty || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(explanation))
            throw new DomainRuleException("risk_signal_invalid", "风险信号必须包含代码、标题和解释。");

        return new RiskSignal
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            Code = code.Trim(),
            Title = title.Trim(),
            Explanation = explanation.Trim(),
            Severity = severity,
            TransactionReferences = (transactionReferences ?? []).Distinct(StringComparer.Ordinal).ToArray()
        };
    }
}
