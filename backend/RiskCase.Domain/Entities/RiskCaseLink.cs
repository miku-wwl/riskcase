using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

public sealed class RiskCaseLink : BaseEntity
{
    public Guid CaseId { get; private set; }
    public string SourceNodeId { get; private set; } = string.Empty;
    public string TargetNodeId { get; private set; } = string.Empty;
    public CaseLinkKind Kind { get; private set; }
    public string Rationale { get; private set; } = string.Empty;
    public string AnalystId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public int CaseVersion { get; private set; }
    public RiskCase Case { get; private set; } = null!;

    private RiskCaseLink() { }

    internal static RiskCaseLink Create(Guid caseId, string sourceNodeId, string targetNodeId, CaseLinkKind kind, string rationale, string analystId, DateTimeOffset occurredAt, int version) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        SourceNodeId = sourceNodeId,
        TargetNodeId = targetNodeId,
        Kind = kind,
        Rationale = rationale,
        AnalystId = analystId,
        OccurredAt = occurredAt,
        CaseVersion = version
    };
}
