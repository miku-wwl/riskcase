using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;
using RiskCaseEntity = RiskCase.Domain.Entities.RiskCase;
using NUnit.Framework;

namespace RiskCase.Domain.UnitTests;

[TestFixture]
public sealed class RiskCaseLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void Investigation_requires_assignment_and_advances_version_for_each_change()
    {
        var riskCase = RiskCaseEntity.Open(Guid.NewGuid());

        var unassigned = Assert.Throws<DomainRuleException>(() => riskCase.StartInvestigation("analyst-1", Now));
        Assert.That(unassigned!.Code, Is.EqualTo("case_assignment_required"));

        riskCase.Assign("analyst-1", Now);
        riskCase.StartInvestigation("analyst-1", Now.AddMinutes(1));
        riskCase.AddNote("analyst-1", "同一交易对手在两个客户账户中重复出现，需要核对业务关系。", Now.AddMinutes(2));
        riskCase.RecordAssessment("analyst-1", RiskAssessmentLevel.High, "交易频率和新交易对手同时出现，且两笔记录关联到另一客户账户。", Now.AddMinutes(3));
        riskCase.RequestReview("analyst-1", Now.AddMinutes(4));

        Assert.That(riskCase.Status, Is.EqualTo(RiskCaseStatus.Reviewing));
        Assert.That(riskCase.Version, Is.EqualTo(6));
        Assert.That(riskCase.Notes, Has.Count.EqualTo(1));
    }

    [Test]
    public void High_risk_case_requires_an_independent_reviewer_and_rationale()
    {
        var riskCase = RiskCaseEntity.Open(Guid.NewGuid());
        riskCase.Assign("analyst-1", Now);
        riskCase.StartInvestigation("analyst-1", Now.AddMinutes(1));
        riskCase.RecordAssessment("analyst-1", RiskAssessmentLevel.Critical, "新设备和新收款人集中出现，同时存在短时间多笔转账。", Now.AddMinutes(2));
        riskCase.RequestReview("analyst-1", Now.AddMinutes(3));

        var sameAnalyst = Assert.Throws<DomainRuleException>(() =>
            riskCase.Decide("analyst-1", RiskDisposition.Escalate, "需要由第二位独立人员核对事实并决定后续处置。", Now.AddMinutes(4)));

        Assert.That(sameAnalyst!.Code, Is.EqualTo("independent_reviewer_required"));

        riskCase.Decide("supervisor-1", RiskDisposition.ReferForAmlReview,
            "已由独立复核人员确认跨账户交易关系需要专项 AML 团队进一步核验。", Now.AddMinutes(5));

        Assert.That(riskCase.Status, Is.EqualTo(RiskCaseStatus.Resolved));
        Assert.That(riskCase.Decisions.Single().AnalystId, Is.EqualTo("supervisor-1"));
        Assert.That(riskCase.Decisions.Single().CaseVersion, Is.EqualTo(6));
    }

    [Test]
    public void Resolved_case_is_immutable_and_notes_count_unicode_runes()
    {
        var riskCase = RiskCaseEntity.Open(Guid.NewGuid());
        riskCase.Assign("analyst-1", Now);
        riskCase.StartInvestigation("analyst-1", Now.AddMinutes(1));
        riskCase.RecordAssessment("analyst-1", RiskAssessmentLevel.Low, "历史交易模式一致，没有发现新的设备、收款人或资金流向变化。", Now.AddMinutes(2));
        riskCase.RequestReview("analyst-1", Now.AddMinutes(3));
        riskCase.Decide("supervisor-1", RiskDisposition.Monitor,
            "当前事实不支持升级处置，保留后续观察并在新信号到达时重新评估。", Now.AddMinutes(4));

        var error = Assert.Throws<DomainRuleException>(() => riskCase.AddNote("analyst-1", "补充记录", Now.AddMinutes(5)));

        Assert.That(error!.Code, Is.EqualTo("case_resolved"));
        Assert.That(riskCase.Status, Is.EqualTo(RiskCaseStatus.Resolved));
    }
}
