using MediatR;
using Moq;
using NUnit.Framework;
using RiskCase.Application.Common.Behaviours;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Application.Common.Security;
using RiskCase.Domain.Constants;

namespace RiskCase.Application.UnitTests;

[TestFixture]
public sealed class AuthorizationBehaviourTests
{
    [Authorize(Roles = Roles.CaseInvestigators)]
    private sealed record ProtectedCommand : IRequest<string>;

    [Test]
    public async Task Analyst_role_reaches_a_case_command()
    {
        var user = new Mock<IUser>();
        user.SetupGet(x => x.Id).Returns("analyst-1");
        user.SetupGet(x => x.Roles).Returns([Roles.Analyst]);
        var identity = new Mock<IIdentityService>();
        var behavior = new AuthorizationBehaviour<ProtectedCommand, string>(user.Object, identity.Object);
        var executed = false;

        var response = await behavior.Handle(new ProtectedCommand(), _ =>
        {
            executed = true;
            return Task.FromResult("accepted");
        }, CancellationToken.None);

        Assert.That(response, Is.EqualTo("accepted"));
        Assert.That(executed, Is.True);
    }

    [Test]
    public void Read_only_auditor_is_rejected_by_the_command_pipeline()
    {
        var user = new Mock<IUser>();
        user.SetupGet(x => x.Id).Returns("auditor-1");
        user.SetupGet(x => x.Roles).Returns([Roles.Auditor]);
        var identity = new Mock<IIdentityService>();
        var behavior = new AuthorizationBehaviour<ProtectedCommand, string>(user.Object, identity.Object);

        Assert.ThrowsAsync<ForbiddenAccessException>(async () =>
            await behavior.Handle(new ProtectedCommand(), _ => Task.FromResult("should not execute"), CancellationToken.None));
    }
}
