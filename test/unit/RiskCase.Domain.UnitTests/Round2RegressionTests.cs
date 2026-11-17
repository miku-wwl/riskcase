using NUnit.Framework;
using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;
using Case = RiskCase.Domain.Entities.RiskCase;

namespace RiskCase.Domain.UnitTests;

public sealed class Round2RegressionTests
{
    private const string Rationale = "核验合成交易记录与交易对手关系后，仍需独立复核解释异常资金流向。";
    private static Case Investigating(string actor = "supervisor")
    {
        var entity = Case.Open(Guid.NewGuid());
        entity.Assign(actor, DateTimeOffset.UtcNow);
        entity.StartInvestigation(actor, DateTimeOffset.UtcNow);
        entity.RecordAssessment(actor, RiskAssessmentLevel.High, Rationale, DateTimeOffset.UtcNow);
        return entity;
    }

    [Test]
    public void Review_assignment_cannot_be_used_to_bypass_independence()
    {
        var entity = Investigating();
        entity.RequestReview("supervisor", DateTimeOffset.UtcNow);
        Assert.Throws<DomainRuleException>(() => entity.Assign("analyst", DateTimeOffset.UtcNow));
    }

    [Test]
    public void Review_freezes_notes_and_links()
    {
        var entity = Investigating();
        entity.RequestReview("supervisor", DateTimeOffset.UtcNow);
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainRuleException>(() => entity.AddNote("supervisor", "new evidence", DateTimeOffset.UtcNow));
            Assert.Throws<DomainRuleException>(() => entity.AddLink("supervisor", "a", "b", CaseLinkKind.Related, Rationale, DateTimeOffset.UtcNow));
        });
    }

    [Test]
    public void Evidence_added_after_assessment_requires_reassessment()
    {
        var entity = Investigating();
        entity.AddNote("supervisor", "later evidence", DateTimeOffset.UtcNow);
        Assert.Throws<DomainRuleException>(() => entity.RequestReview("supervisor", DateTimeOffset.UtcNow));
    }

    public static IEnumerable<TestCaseData> Transitions()
    {
        foreach (var state in Enum.GetValues<RiskCaseStatus>())
        foreach (var operation in new[] { "assign", "start", "note", "link", "assess", "review", "resume", "decide" })
        {
            var valid = operation switch {
                "assign" => state is RiskCaseStatus.New or RiskCaseStatus.Investigating,
                "start" => state == RiskCaseStatus.New,
                "note" or "link" or "assess" or "review" => state == RiskCaseStatus.Investigating,
                "resume" or "decide" => state == RiskCaseStatus.Reviewing,
                _ => false
            };
            yield return new TestCaseData(state, operation, valid);
        }
    }

    [TestCaseSource(nameof(Transitions))]
    public void State_matrix_preserves_versions_on_rejected_transitions(RiskCaseStatus state, string operation, bool valid)
    {
        var c = Case.Open(Guid.NewGuid()); var now = DateTimeOffset.UtcNow;
        c.Assign("author", now);
        if (state != RiskCaseStatus.New) { c.StartInvestigation("author", now); c.RecordAssessment("author", RiskAssessmentLevel.High, Rationale, now); }
        if (state is RiskCaseStatus.Reviewing or RiskCaseStatus.Resolved) c.RequestReview("author", now);
        if (state == RiskCaseStatus.Resolved) c.Decide("reviewer", RiskDisposition.Monitor, Rationale, now);
        var version = c.Version;
        TestDelegate action = operation switch {
            "assign" => () => c.Assign("second", now), "start" => () => c.StartInvestigation("author", now),
            "note" => () => c.AddNote("author", Rationale, now),
            "link" => () => c.AddLink("author", "a", "b", CaseLinkKind.Related, Rationale, now),
            "assess" => () => c.RecordAssessment("author", RiskAssessmentLevel.Low, Rationale, now),
            "review" => () => c.RequestReview("author", now), "resume" => () => c.ResumeInvestigation("author", Rationale, now),
            "decide" => () => c.Decide("independent", RiskDisposition.Escalate, Rationale, now),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        if (valid) { Assert.DoesNotThrow(action); Assert.That(c.Version, Is.EqualTo(version + 1)); }
        else { Assert.Throws<DomainRuleException>(action); Assert.That(c.Version, Is.EqualTo(version)); }
    }

    [Test]
    public void Assessment_history_and_decision_bind_to_the_submitted_basis_after_return_and_reassignment()
    {
        var c = Investigating("author"); var first = c.Assessments.Single(); var now = DateTimeOffset.UtcNow;
        c.RequestReview("author", now); c.ResumeInvestigation("author", Rationale, now); c.Assign("new-author", now);
        Assert.Throws<DomainRuleException>(() => c.RequestReview("new-author", now));
        var second = c.RecordAssessment("new-author", RiskAssessmentLevel.Low, Rationale + "重新评估。", now);
        c.RequestReview("new-author", now);
        Assert.Throws<DomainRuleException>(() => c.Decide("new-author", RiskDisposition.Monitor, Rationale, now));
        var decision = c.Decide("reviewer", RiskDisposition.Monitor, Rationale, now);
        Assert.Multiple(() => { Assert.That(c.Assessments.Count, Is.EqualTo(2)); Assert.That(first.AssessorId, Is.EqualTo("author"));
            Assert.That(first.Level, Is.EqualTo(RiskAssessmentLevel.High)); Assert.That(decision.AssessmentId, Is.EqualTo(second.Id)); });
    }
}
