using NUnit.Framework;
using RiskCase.Application.RiskCases;

namespace RiskCase.Application.UnitTests;

public sealed class ReviewReturnValidationTests
{
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("short")]
    [TestCase("oversized")]
    public void Present_but_invalid_reasons_are_rejected_before_execution(string reason)
    {
        var command=new ResumeRiskCaseCommand(Guid.NewGuid().ToString(),Guid.NewGuid(),5,reason=="oversized"?new string('x',1501):reason);
        Assert.That(new ResumeRiskCaseCommandValidator().Validate(command).IsValid,Is.False);
    }
    [Test]
    public void Bodyless_command_can_reach_historical_replay_but_new_write_handler_requires_reason()
    {
        Assert.That(new ResumeRiskCaseCommandValidator().Validate(new ResumeRiskCaseCommand(Guid.NewGuid().ToString(),Guid.NewGuid(),5)).IsValid,Is.True);
    }
    [TestCase(20)]
    [TestCase(1500)]
    public void Trimmed_unicode_scalar_boundaries_match_domain_and_browser(int length)
    {
        var reason="  "+string.Concat(Enumerable.Repeat("𠮷",length))+"\n";
        Assert.That(new ResumeRiskCaseCommandValidator().Validate(new ResumeRiskCaseCommand(Guid.NewGuid().ToString(),Guid.NewGuid(),5,reason)).IsValid,Is.True);
    }
}
