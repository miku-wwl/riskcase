using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Application.RiskCases;

namespace RiskCase.Application.Common.Behaviours;

public interface IIdempotentRequest
{
    string IdempotencyKey { get; }
}

public sealed class IdempotencyBehaviour<TRequest, TResponse>(IIdempotencyStore store)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotent)
            return next();

        if (!Guid.TryParseExact(idempotent.IdempotencyKey, "D", out _))
            throw new ApplicationFault(400, "idempotency_key_required", "写操作需要 UUID 格式的 Idempotency-Key。");

        return store.ExecuteAsync(idempotent.IdempotencyKey, Fingerprint(request), () => next(), cancellationToken,
            (fingerprint, json) => CanReplayLegacy(request, fingerprint, json));
    }

    public static string Fingerprint(object request, bool includeCommandType = true, bool omitReturnReason = false)
    {
        var json = JsonSerializer.SerializeToElement(request, request.GetType());
        var sorted = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in json.EnumerateObject().Where(p => !StringComparer.OrdinalIgnoreCase.Equals(p.Name, nameof(IIdempotentRequest.IdempotencyKey))))
        {
            if (omitReturnReason && property.Name == nameof(ResumeRiskCaseCommand.Reason)) continue;
            sorted.Add(property.Name, property.Value.Clone());
        }
        var identity = includeCommandType ? request.GetType().FullName + ":" : "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + JsonSerializer.Serialize(sorted))));
    }

    private static bool CanReplayLegacy(TRequest request, string fingerprint, string responseJson)
    {
        var compatible = fingerprint == Fingerprint(request, includeCommandType: false);
        if (request is ResumeRiskCaseCommand { Reason: null })
            compatible |= fingerprint == Fingerprint(request, omitReturnReason: true)
                || fingerprint == Fingerprint(request, includeCommandType: false, omitReturnReason: true);
        if (!compatible) return false;
        using var document = JsonDocument.Parse(responseJson);
        var response = document.RootElement;
        if (request is IngestAlertCommand intake)
            return response.TryGetProperty("Alert", out var alert)
                && alert.GetProperty("ExternalAlertId").GetString() == intake.ExternalAlertId.Trim()
                && alert.GetProperty("SourceSystem").GetString() == intake.SourceSystem.Trim();
        if (request is not IVersionedRiskCaseCommand command) return false;
        var operation = request switch {
            AssignRiskCaseCommand => "AnalystAssigned", StartRiskCaseCommand => "InvestigationStarted",
            AddCaseNoteCommand => "NoteAdded", AddCaseLinkCommand => "RelationshipRecorded",
            AssessRiskCaseCommand => "AssessmentRecorded", RequestCaseReviewCommand => "ReviewRequested",
            ResumeRiskCaseCommand => "InvestigationResumed", DecideRiskCaseCommand => "DispositionRecorded", _ => null
        };
        if (operation is null || !response.TryGetProperty("Id", out var id) || id.GetGuid() != command.CaseId
            || !response.TryGetProperty("Version", out var version) || version.GetInt32() != (long)command.ExpectedVersion + 1
            || !response.TryGetProperty("AuditHistory", out var audit)) return false;
        return audit.EnumerateArray().Any(item => item.GetProperty("Version").GetInt32() == version.GetInt32()
            && item.GetProperty("Operation").GetString() == operation);
    }
}
