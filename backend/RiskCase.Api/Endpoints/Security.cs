using Microsoft.AspNetCore.Antiforgery;

namespace RiskCase.Api.Endpoints;

public sealed class Security : IEndpointGroup
{
    public static string RoutePrefix => "/api/security";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/antiforgery", IssueAntiforgeryToken)
            .AllowAnonymous()
            .WithName("IssueAntiforgeryToken")
            .Produces<AntiforgeryTokenView>();
    }

    public static AntiforgeryTokenView IssueAntiforgeryToken(IAntiforgery antiforgery, HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return new AntiforgeryTokenView(tokens.RequestToken ?? throw new InvalidOperationException("Could not issue an anti-forgery token."));
    }
}

public sealed record AntiforgeryTokenView(string RequestToken);
