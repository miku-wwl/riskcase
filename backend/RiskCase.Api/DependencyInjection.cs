using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Constants;
using RiskCase.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddWebServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddScoped<IUser, CurrentUser>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi(options =>
        {
            options.AddOperationTransformer<ApiExceptionOperationTransformer>();
            options.AddOperationTransformer<IdentityApiOperationTransformer>();
        });
        builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RiskCaseAccess", policy => policy.RequireRole(Roles.Analyst, Roles.SeniorAnalyst, Roles.Supervisor, Roles.Auditor));
            options.AddPolicy("RiskAnalyst", policy => policy.RequireRole(Roles.Analyst, Roles.SeniorAnalyst, Roles.Supervisor));
            options.AddPolicy("RiskSupervisor", policy => policy.RequireRole(Roles.Supervisor));
        });
        builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
        builder.Services.AddScoped<AntiforgeryEndpointFilter>();
        builder.Services.AddHealthChecks();
    }
}
