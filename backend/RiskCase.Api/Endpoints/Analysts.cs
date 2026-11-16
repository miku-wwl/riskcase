using RiskCase.Domain.Constants;
using RiskCase.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Application.Common.Exceptions;

namespace RiskCase.Api.Endpoints;

public sealed class Analysts : IEndpointGroup
{
    public static string RoutePrefix => "/api/analysts";

    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization("RiskSupervisor");
        group.MapGet("/", ListAnalysts).WithName("ListAnalysts").Produces<IReadOnlyList<AnalystView>>();
    }

    public static async Task<IReadOnlyList<AnalystView>> ListAnalysts(UserManager<ApplicationUser> users, IUser user, IIdentityService identity)
    {
        if (user.Id is null || !await identity.IsInRoleAsync(user.Id, Roles.Supervisor)) throw new ForbiddenAccessException();
        var analysts = (await users.GetUsersInRoleAsync(Roles.Analyst))
            .Concat(await users.GetUsersInRoleAsync(Roles.SeniorAnalyst))
            .Concat(await users.GetUsersInRoleAsync(Roles.Supervisor))
            .DistinctBy(x => x.Id);
        var eligible = new List<AnalystView>();
        foreach (var analyst in analysts.OrderBy(x => x.Email))
            if (!await users.IsLockedOutAsync(analyst)) eligible.Add(new(analyst.Id, analyst.Email ?? analyst.UserName ?? ""));
        return eligible;
    }
}

public sealed record AnalystView(string Id, string Email);
