using RiskCase.Domain.Constants;
using RiskCase.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace RiskCase.Api.Endpoints;

public sealed class Analysts : IEndpointGroup
{
    public static string RoutePrefix => "/api/analysts";

    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization("RiskSupervisor");
        group.MapGet("/", ListAnalysts).WithName("ListAnalysts").Produces<IReadOnlyList<AnalystView>>();
    }

    public static async Task<IReadOnlyList<AnalystView>> ListAnalysts(UserManager<ApplicationUser> users)
    {
        var analysts = (await users.GetUsersInRoleAsync(Roles.Analyst))
            .Concat(await users.GetUsersInRoleAsync(Roles.SeniorAnalyst))
            .DistinctBy(x => x.Id);
        return analysts.OrderBy(x => x.Email).Select(x => new AnalystView(x.Id, x.Email ?? x.UserName ?? "")).ToArray();
    }
}

public sealed record AnalystView(string Id, string Email);
