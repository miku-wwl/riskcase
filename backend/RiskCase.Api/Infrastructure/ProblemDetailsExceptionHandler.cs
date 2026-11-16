using RiskCase.Application.Common.Exceptions;
using RiskCase.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace RiskCase.Api.Infrastructure;

public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ApplicationFault fault => Create(fault.StatusCode, fault.Code, fault.Message),
            DomainRuleException domain => Create(StatusCodes.Status409Conflict, domain.Code, domain.Message),
            ValidationException validation => new ValidationProblemDetails(validation.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request validation failed.",
                Extensions = { ["code"] = "validation_failed" }
            },
            NotFoundException missing => Create(StatusCodes.Status404NotFound, "resource_not_found", missing.Message),
            UnauthorizedAccessException => Create(StatusCodes.Status401Unauthorized, "unauthorized", "Authentication is required."),
            ForbiddenAccessException => Create(StatusCodes.Status403Forbidden, "forbidden", "The current user cannot perform this operation."),
            DbUpdateConcurrencyException => Create(StatusCodes.Status412PreconditionFailed, "version_conflict", "The record changed in another session. Reload it before retrying."),
            _ => null
        };

        if (problem is null)
            return false;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static ProblemDetails Create(int status, string code, string detail) => new()
    {
        Status = status,
        Title = status switch
        {
            StatusCodes.Status400BadRequest => "Request could not be accepted.",
            StatusCodes.Status401Unauthorized => "Authentication required.",
            StatusCodes.Status403Forbidden => "Operation is not allowed.",
            StatusCodes.Status404NotFound => "Resource not found.",
            StatusCodes.Status409Conflict => "Business rule conflict.",
            StatusCodes.Status412PreconditionFailed => "The record version is stale.",
            StatusCodes.Status428PreconditionRequired => "A request precondition is required.",
            _ => "Request failed."
        },
        Detail = detail,
        Extensions = { ["code"] = code }
    };
}
