using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

public sealed class RiskAlert : BaseAuditableEntity
{
    private readonly List<RiskSignal> _signals = [];

    public string ExternalAlertId { get; private set; } = string.Empty;
    public string SourceSystem { get; private set; } = string.Empty;
    public string IngestionFingerprint { get; private set; } = string.Empty;
    public RiskAlertCategory Category { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }
    public RiskSeverity Severity { get; private set; }
    public int? RiskScore { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid AccountId { get; private set; }
    public Customer Customer { get; private set; } = null!;
    public Account Account { get; private set; } = null!;
    public IReadOnlyCollection<RiskSignal> Signals => _signals.AsReadOnly();
    public ICollection<RiskTransaction> Transactions { get; private set; } = new List<RiskTransaction>();

    private RiskAlert() { }

    public static RiskAlert Receive(
        Guid id,
        string externalAlertId,
        string sourceSystem,
        string ingestionFingerprint,
        RiskAlertCategory category,
        DateTimeOffset detectedAt,
        RiskSeverity severity,
        int? riskScore,
        Guid customerId,
        Guid accountId,
        IEnumerable<RiskSignal> signals)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(externalAlertId) || string.IsNullOrWhiteSpace(sourceSystem) || string.IsNullOrWhiteSpace(ingestionFingerprint))
            throw new DomainRuleException("external_alert_id_required", "来源系统与外部告警编号不能为空。");
        if (customerId == Guid.Empty || accountId == Guid.Empty)
            throw new DomainRuleException("alert_subject_required", "告警必须关联客户与账户。");
        if (riskScore is < 0 or > 100)
            throw new DomainRuleException("risk_score_out_of_range", "风险分数必须在 0 至 100 之间。");

        var alert = new RiskAlert
        {
            Id = id,
            ExternalAlertId = externalAlertId.Trim(),
            SourceSystem = sourceSystem.Trim(),
            IngestionFingerprint = ingestionFingerprint,
            Category = category,
            DetectedAt = detectedAt,
            Severity = severity,
            RiskScore = riskScore,
            CustomerId = customerId,
            AccountId = accountId
        };
        foreach (var signal in signals)
        {
            if (signal.AlertId != alert.Id)
                throw new DomainRuleException("risk_signal_alert_mismatch", "风险信号必须属于当前告警。");
            alert._signals.Add(signal);
        }
        if (alert._signals.Count == 0)
            throw new DomainRuleException("risk_signal_required", "上游告警至少需要一个可解释风险信号。");

        return alert;
    }
}
