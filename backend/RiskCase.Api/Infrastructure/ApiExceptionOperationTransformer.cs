using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RiskCase.Api.Infrastructure;

/// <summary>
/// Adds standard error responses to every OpenAPI operation. A 400 Bad Request is added to all
/// operations because every request passes through <c>ValidationBehaviour</c> in the MediatR
/// pipeline. 401 Unauthorized and 403 Forbidden are added only to operations that carry
/// <see cref="IAuthorizeData"/> metadata.
/// </summary>
internal sealed class ApiExceptionOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Responses ??= [];
        operation.Responses.TryAdd("400", new OpenApiResponse { Description = "Bad Request" });
        var path = context.Description.RelativePath ?? "";
        if (path.StartsWith("api/cases", StringComparison.Ordinal))
        {
            operation.Responses.TryAdd("404", new OpenApiResponse { Description = "Resource not found" });
            if (context.Description.HttpMethod is not "GET")
            {
                operation.Responses.TryAdd("409", new OpenApiResponse { Description = "Business or idempotency conflict" });
                operation.Responses.TryAdd("412", new OpenApiResponse { Description = "Stale case version" });
                operation.Responses.TryAdd("428", new OpenApiResponse { Description = "If-Match required" });
                operation.Responses.TryAdd("503", new OpenApiResponse { Description = "Transaction rolled back; retry original intent" });
            }
        }
        if (path.StartsWith("api/ingestion", StringComparison.Ordinal))
        {
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Ingestion credential required" });
            operation.Responses.TryAdd("409", new OpenApiResponse { Description = "External alert or idempotency conflict" });
            operation.Responses.TryAdd("503", new OpenApiResponse { Description = "Ingestion unavailable or transaction retry required" });
        }

        var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
            .Any(m => m is IAuthorizeData);

        if (requiresAuth)
        {
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
        }

        return Task.CompletedTask;
    }
}
