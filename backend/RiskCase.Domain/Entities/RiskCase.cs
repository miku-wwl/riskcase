using System.Text;
using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

public sealed class RiskCase : BaseAuditableEntity
{
    private readonly List<RiskCaseNote> _notes = [];
    private readonly List<RiskCaseLink> _links = [];
    private readonly List<RiskDecision> _decisions = [];

    public Guid AlertId { get; private set; }
    public RiskCaseStatus Status { get; private set; }
    public string? AssignedAnalystId { get; private set; }
    public int Version { get; private set; }
    public RiskAssessmentLevel? AssessmentLevel { get; private set; }
    public string? AssessmentRationale { get; private set; }
    public DateTimeOffset? AssessedAt { get; private set; }
    public RiskAlert Alert { get; private set; } = null!;
    public IReadOnlyCollection<RiskCaseNote> Notes => _notes.AsReadOnly();
    public IReadOnlyCollection<RiskCaseLink> Links => _links.AsReadOnly();
    public IReadOnlyCollection<RiskDecision> Decisions => _decisions.AsReadOnly();

    private RiskCase() { }

    public static RiskCase Open(Guid alertId)
    {
        if (alertId == Guid.Empty)
            throw new DomainRuleException("alert_required", "风控档案必须关联来源告警。");
        return new RiskCase
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            Status = RiskCaseStatus.New,
            Version = 1
        };
    }

    public void Assign(string analystId, DateTimeOffset now)
    {
        EnsureNotResolved();
        if (string.IsNullOrWhiteSpace(analystId))
            throw new DomainRuleException("analyst_required", "必须指定负责分析员。");
        if (StringComparer.Ordinal.Equals(AssignedAnalystId, analystId))
            throw new DomainRuleException("assignment_unchanged", "当前档案已分配给该分析员。");
        AssignedAnalystId = analystId;
        Advance(now);
    }

    public void StartInvestigation(string analystId, DateTimeOffset now)
    {
        EnsureAssignedAnalyst(analystId);
        Require(Status == RiskCaseStatus.New, "invalid_case_transition", "只有新档案可以开始调查。");
        Status = RiskCaseStatus.Investigating;
        Advance(now);
    }

    public RiskCaseNote AddNote(string analystId, string text, DateTimeOffset now)
    {
        EnsureAssignedAnalyst(analystId);
        EnsureEditable();
        var normalized = text.Trim();
        Require(CountRunes(normalized) is >= 1 and <= 2000, "note_length_invalid", "调查笔记须为 1 至 2000 个字符。");
        Advance(now);
        var note = RiskCaseNote.Create(Id, analystId, normalized, now, Version);
        _notes.Add(note);
        return note;
    }

    public RiskCaseLink AddLink(string analystId, string sourceNodeId, string targetNodeId, CaseLinkKind kind, string rationale, DateTimeOffset now)
    {
        EnsureAssignedAnalyst(analystId);
        EnsureEditable();
        Require(Enum.IsDefined(kind), "link_kind_invalid", "关系类型无效。");
        Require(!string.IsNullOrWhiteSpace(sourceNodeId) && !string.IsNullOrWhiteSpace(targetNodeId), "link_endpoint_required", "关系两端不能为空。");
        Require(!StringComparer.Ordinal.Equals(sourceNodeId, targetNodeId), "self_link_not_allowed", "不能把实体关联到自身。");
        var explanation = rationale.Trim();
        Require(CountRunes(explanation) is >= 20 and <= 500, "link_rationale_length_invalid", "关系解释须为 20 至 500 个字符。");

        if (StringComparer.Ordinal.Compare(sourceNodeId, targetNodeId) > 0)
            (sourceNodeId, targetNodeId) = (targetNodeId, sourceNodeId);
        Require(!_links.Any(x => x.SourceNodeId == sourceNodeId && x.TargetNodeId == targetNodeId), "duplicate_case_link", "该实体对已有关联记录。");

        Advance(now);
        var link = RiskCaseLink.Create(Id, sourceNodeId, targetNodeId, kind, explanation, analystId, now, Version);
        _links.Add(link);
        return link;
    }

    public void RecordAssessment(string analystId, RiskAssessmentLevel level, string rationale, DateTimeOffset now)
    {
        EnsureAssignedAnalyst(analystId);
        Require(Status == RiskCaseStatus.Investigating, "assessment_requires_investigation", "只能在调查中记录风险评估。");
        Require(Enum.IsDefined(level), "assessment_level_invalid", "风险等级无效。");
        var explanation = rationale.Trim();
        Require(CountRunes(explanation) is >= 20 and <= 1500, "assessment_rationale_length_invalid", "评估依据须为 20 至 1500 个字符。");
        AssessmentLevel = level;
        AssessmentRationale = explanation;
        AssessedAt = now;
        Advance(now);
    }

    public void RequestReview(string analystId, DateTimeOffset now)
    {
        EnsureAssignedAnalyst(analystId);
        Require(Status == RiskCaseStatus.Investigating, "invalid_case_transition", "只有调查中的档案可以提交复核。");
        Require(AssessmentLevel is not null && !string.IsNullOrWhiteSpace(AssessmentRationale), "assessment_required", "提交复核前必须完成风险评估。");
        Status = RiskCaseStatus.Reviewing;
        Advance(now);
    }

    public void ResumeInvestigation(string analystId, DateTimeOffset now)
    {
        EnsureAssignedAnalyst(analystId);
        Require(Status == RiskCaseStatus.Reviewing, "invalid_case_transition", "只有复核中的档案可以退回调查。");
        Status = RiskCaseStatus.Investigating;
        Advance(now);
    }

    public RiskDecision Decide(string analystId, RiskDisposition disposition, string rationale, DateTimeOffset now)
    {
        EnsureNotResolved();
        Require(Status == RiskCaseStatus.Reviewing, "decision_requires_review", "风险处置必须在复核阶段完成。");
        Require(!StringComparer.Ordinal.Equals(AssignedAnalystId, analystId), "independent_reviewer_required", "最终处置必须由非当前负责分析员的复核人员作出。");
        Require(AssessmentLevel is not null, "assessment_required", "必须先完成风险评估。");
        Require(Enum.IsDefined(disposition), "disposition_invalid", "处置结果无效。");
        var explanation = rationale.Trim();
        Require(CountRunes(explanation) is >= 20 and <= 2000, "decision_rationale_length_invalid", "处置理由须为 20 至 2000 个字符。");
        Status = RiskCaseStatus.Resolved;
        Advance(now);
        var decision = RiskDecision.Record(Id, disposition, explanation, analystId, now, Version);
        _decisions.Add(decision);
        return decision;
    }

    public void EnsureAssignedAnalyst(string analystId)
    {
        Require(StringComparer.Ordinal.Equals(AssignedAnalystId, analystId), "case_assignment_required", "只有当前负责分析员可以修改该档案。");
        EnsureNotResolved();
    }

    private void EnsureEditable() => Require(Status is RiskCaseStatus.Investigating or RiskCaseStatus.Reviewing, "case_not_editable", "当前档案状态不允许此项调查操作。");
    private void EnsureNotResolved() => Require(Status != RiskCaseStatus.Resolved, "case_resolved", "已完成处置的档案为只读状态。");
    private void Advance(DateTimeOffset now)
    {
        Version = checked(Version + 1);
        LastModified = now;
    }

    private static int CountRunes(string value) => value.EnumerateRunes().Count();

    private static void Require(bool condition, string code, string message)
    {
        if (!condition)
            throw new DomainRuleException(code, message);
    }
}
