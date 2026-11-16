using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

public sealed class RiskDecision : BaseEntity
{
    public Guid CaseId { get; private set; }
    public Guid? AssessmentId { get; private set; }
    public RiskDisposition Disposition { get; private set; }
    public string Rationale { get; private set; } = string.Empty;
    public string AnalystId { get; private set; } = string.Empty;
    public DateTimeOffset DecidedAt { get; private set; }
    public int CaseVersion { get; private set; }
    public RiskCase Case { get; private set; } = null!;

    private RiskDecision() { }

    internal static RiskDecision Record(Guid caseId, Guid assessmentId, RiskDisposition disposition, string rationale, string analystId, DateTimeOffset decidedAt, int version) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        AssessmentId = assessmentId,
        Disposition = disposition,
        Rationale = rationale,
        AnalystId = analystId,
        DecidedAt = decidedAt,
        CaseVersion = version
    };
}
