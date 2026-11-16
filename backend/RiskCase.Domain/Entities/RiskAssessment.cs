using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

/// <summary>An append-only statement of an investigator's assessment of a fixed case basis.</summary>
public sealed class RiskAssessment : BaseEntity
{
    public Guid CaseId { get; private set; }
    public RiskAssessmentLevel Level { get; private set; }
    public string Rationale { get; private set; } = "";
    public string? AssessorId { get; private set; }
    public DateTimeOffset AssessedAt { get; private set; }
    public int CaseVersion { get; private set; }
    public int BasisVersion { get; private set; }
    public bool IsLegacy { get; private set; }
    public RiskCase Case { get; private set; } = null!;
    private RiskAssessment() { }
    internal static RiskAssessment Record(Guid caseId, RiskAssessmentLevel level, string rationale, string actor,
        DateTimeOffset now, int version, int basisVersion) => new()
    {
        Id = Guid.NewGuid(), CaseId = caseId, Level = level, Rationale = rationale,
        AssessorId = actor, AssessedAt = now, CaseVersion = version, BasisVersion = basisVersion
    };
}
