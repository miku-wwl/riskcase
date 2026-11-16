using Microsoft.AspNetCore.Antiforgery;

namespace RiskCase.Api.Infrastructure;

public sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            return await next(context);

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Anti-forgery validation failed.",
                detail: "Refresh the security token and retry the request.",
                extensions: new Dictionary<string, object?> { ["code"] = "csrf_validation_failed" });
        }

        return await next(context);
    }
}
