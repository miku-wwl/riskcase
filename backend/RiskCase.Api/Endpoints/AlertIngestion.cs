using System.Security.Cryptography;
using System.Text;
using RiskCase.Application.RiskCases;
using Microsoft.AspNetCore.Mvc;

namespace RiskCase.Api.Endpoints;

public sealed class AlertIngestion : IEndpointGroup
{
    public static string RoutePrefix => "/api/ingestion/alerts";

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Ingest).WithName("IngestRiskAlert").Produces<RiskCaseView>(StatusCodes.Status201Created);
    }

    public static async Task<IResult> Ingest(IngestAlertRequest body,
        [FromHeader(Name = "X-Alert-Ingestion-Key")] string? suppliedKey,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IConfiguration configuration,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var expectedKey = configuration["RiskCase:IngestionApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Alert ingestion is not configured.", extensions: new Dictionary<string, object?> { ["code"] = "ingestion_not_configured" });
        if (string.IsNullOrWhiteSpace(suppliedKey) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedKey), Encoding.UTF8.GetBytes(suppliedKey)))
            return Results.Unauthorized();

        var response = await sender.Send(new IngestAlertCommand(idempotencyKey ?? "", body.ExternalAlertId, body.SourceSystem,
            body.Category, body.DetectedAt, body.Severity, body.RiskScore, body.CustomerId, body.AccountId, body.Signals, body.Transactions), cancellationToken);
        return Results.Created($"/api/cases/{response.Id}", response);
    }
}

public sealed record IngestAlertRequest(
    string ExternalAlertId,
    string SourceSystem,
    RiskCase.Domain.Enums.RiskAlertCategory Category,
    DateTimeOffset DetectedAt,
    RiskCase.Domain.Enums.RiskSeverity Severity,
    int? RiskScore,
    Guid CustomerId,
    Guid AccountId,
    IReadOnlyList<RiskSignalInput> Signals,
    IReadOnlyList<RiskTransactionInput> Transactions);
