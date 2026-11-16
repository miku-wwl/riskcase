namespace RiskCase.Api.Infrastructure;

/// <summary>
/// Defines a group of related Minimal API endpoints.
/// Implementations are automatically discovered and registered as a route group with a matching
/// OpenAPI tag. By default the route prefix is <c>/api/{ClassName}</c>; override
/// <see cref="RoutePrefix"/> to use a custom path, including nested resource paths such as
/// <c>/api/cases/{caseId}/notes</c>.
/// </summary>
public interface IEndpointGroup
{
    /// <summary>
    /// The route prefix for this endpoint group.
    /// Defaults to <c>/api/{ClassName}</c>. Override when a business resource uses a stable public route.
    /// </summary>
    static virtual string? RoutePrefix => null;

    static abstract void Map(RouteGroupBuilder groupBuilder);
}
