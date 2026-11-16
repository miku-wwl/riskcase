using RiskCase.Application.Common.Interfaces;

namespace RiskCase.Api.Endpoints;

public sealed class Session : IEndpointGroup
{
    public static string RoutePrefix => "/api/session";

    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();
        group.MapGet("/", (IUser user) => Results.Ok(new SessionView(user.Id!, user.Email ?? "", user.Roles ?? [])))
            .WithName("GetSession").Produces<SessionView>();
    }
}

public sealed record SessionView(string Id, string Email, IReadOnlyList<string> Roles);
