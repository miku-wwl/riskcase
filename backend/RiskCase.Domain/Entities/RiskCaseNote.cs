using RiskCase.Domain.Common;

namespace RiskCase.Domain.Entities;

public sealed class RiskCaseNote : BaseEntity
{
    public Guid CaseId { get; private set; }
    public string AnalystId { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public int CaseVersion { get; private set; }
    public RiskCase Case { get; private set; } = null!;

    private RiskCaseNote() { }

    internal static RiskCaseNote Create(Guid caseId, string analystId, string text, DateTimeOffset occurredAt, int version) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        AnalystId = analystId,
        Text = text,
        OccurredAt = occurredAt,
        CaseVersion = version
    };
}
