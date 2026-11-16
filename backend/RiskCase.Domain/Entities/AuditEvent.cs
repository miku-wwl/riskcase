using RiskCase.Domain.Common;

namespace RiskCase.Domain.Entities;

public sealed class AuditEvent : BaseEntity
{
    public Guid CaseId { get; private set; }
    public int CaseVersion { get; private set; }
    public string ActorId { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = "{}";
    public DateTimeOffset OccurredAt { get; private set; }
    public RiskCase Case { get; private set; } = null!;

    private AuditEvent() { }

    public static AuditEvent Record(Guid caseId, int version, string actorId, string operation, string detailsJson, DateTimeOffset occurredAt) => new()
    {
        Id = Guid.NewGuid(),
        CaseId = caseId,
        CaseVersion = version,
        ActorId = actorId,
        Operation = operation,
        DetailsJson = detailsJson,
        OccurredAt = occurredAt
    };
}
