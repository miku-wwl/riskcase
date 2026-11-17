using System.Reflection;
using NUnit.Framework;
using RiskCase.Domain.Common;
using RiskCase.Domain.Entities;
using RiskCase.Domain.Enums;
using Case = RiskCase.Domain.Entities.RiskCase;

namespace RiskCase.Domain.UnitTests;

public sealed class Round3IntegrityTests
{
    private const string Reason="The beneficiary ownership must be verified before resubmission for review.";
    private static Case Reviewing()
    {
        var c=Case.Open(Guid.NewGuid());c.Assign("author",DateTimeOffset.UtcNow);c.StartInvestigation("author",DateTimeOffset.UtcNow);
        c.RecordAssessment("author",RiskAssessmentLevel.High,Reason,DateTimeOffset.UtcNow);c.RequestReview("author",DateTimeOffset.UtcNow);return c;
    }
    [TestCase("old-one")]
    [TestCase("old-two")]
    [TestCase(null)]
    public void Legacy_participation_is_independent_of_current_assignment(string? legacyAuthor)
    {
        var c=Reviewing();
        // Model a row hydrated by EF, without exposing a domain API for manufacturing legacy facts.
        var row=(LegacyAssessorParticipation)Activator.CreateInstance(typeof(LegacyAssessorParticipation),nonPublic:true)!;
        typeof(LegacyAssessorParticipation).GetProperty(nameof(LegacyAssessorParticipation.AssessorId))!.SetValue(row,legacyAuthor);
        ((List<LegacyAssessorParticipation>)typeof(Case).GetField("_legacyAssessorParticipation",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(c)!).Add(row);
        var error=Assert.Throws<DomainRuleException>(()=>c.Decide(legacyAuthor??"independent",RiskDisposition.Monitor,Reason,DateTimeOffset.UtcNow));
        Assert.That(error!.Code,Is.EqualTo(legacyAuthor is null ? "legacy_assessor_identity_unavailable" : "independent_reviewer_required"));
        if(legacyAuthor is not null) Assert.DoesNotThrow(()=>c.Decide("independent",RiskDisposition.Monitor,Reason,DateTimeOffset.UtcNow));
    }
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("short")]
    [TestCase("oversized")]
    public void Invalid_return_reason_cannot_change_frozen_case(string reason)
    {
        var c=Reviewing();var version=c.Version;var assessment=c.CurrentAssessmentId;
        Assert.That(Assert.Throws<DomainRuleException>(()=>c.ResumeInvestigation("author",reason=="oversized"?new string('x',1501):reason,DateTimeOffset.UtcNow))!.Code,Is.EqualTo("return_reason_length_invalid"));
        Assert.Multiple(()=>{Assert.That(c.Status,Is.EqualTo(RiskCaseStatus.Reviewing));Assert.That(c.Version,Is.EqualTo(version));Assert.That(c.CurrentAssessmentId,Is.EqualTo(assessment));});
    }
    [Test]
    public void Return_accepts_trimmed_reason_and_retains_assessment_history_but_invalidates_review()
    {
        var c=Reviewing();var version=c.Version;
        Assert.That(c.ResumeInvestigation("author","  "+Reason+"\n",DateTimeOffset.UtcNow),Is.EqualTo(Reason));
        Assert.Multiple(()=>{Assert.That(c.Version,Is.EqualTo(version+1));Assert.That(c.Status,Is.EqualTo(RiskCaseStatus.Investigating));Assert.That(c.CurrentAssessmentId,Is.Null);Assert.That(c.ReviewedAssessmentId,Is.Null);Assert.That(c.Assessments.Count,Is.EqualTo(1));});
        Assert.Throws<DomainRuleException>(()=>c.RequestReview("author",DateTimeOffset.UtcNow));
    }
}
