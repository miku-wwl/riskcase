using RiskCase.Domain.Common;

namespace RiskCase.Domain.Entities;

/// <summary>Migration provenance, not a fabricated historical risk assessment. Null means identity unavailable.</summary>
public sealed class LegacyAssessorParticipation : BaseEntity
{
    public Guid CaseId { get; private set; }
    public int CaseVersion { get; private set; }
    public string? AssessorId { get; private set; }
    public Guid? SourceAuditId { get; private set; }
    public Guid? SourceAssessmentId { get; private set; }
    public RiskCase Case { get; private set; } = null!;
    private LegacyAssessorParticipation() { }
}
