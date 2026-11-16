using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.RiskCases;
using RiskCase.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace RiskCase.Api.Endpoints;

public sealed class RiskCases : IEndpointGroup
{
    public static string RoutePrefix => "/api/cases";

    public static void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization("RiskCaseAccess");
        group.AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapGet("/", ListCases).WithName("ListRiskCases").Produces<RiskCaseQueuePage>();
        group.MapGet("/{id:guid}", GetCase).WithName("GetRiskCase").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/assignment", Assign).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/start", Start).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/notes", AddNote).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/links", AddLink).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPut("/{id:guid}/assessment", Assess).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/review", RequestReview).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/resume", Resume).RequireAuthorization("RiskAnalyst").Produces<RiskCaseView>();
        group.MapPost("/{id:guid}/decision", Decide).RequireAuthorization("RiskSupervisor").Produces<RiskCaseView>();
    }

    public static Task<RiskCaseQueuePage> ListCases(
        string? status, string? category, string? severity, string? search, bool assignedToMe,
        ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 25) =>
        sender.Send(new GetRiskCasesQuery(status, category, severity, search, assignedToMe, page, pageSize), cancellationToken);

    public static Task<RiskCaseView> GetCase(Guid id, ISender sender, CancellationToken cancellationToken) =>
        sender.Send(new GetRiskCaseQuery(id), cancellationToken);

    public static async Task<IResult> Assign(Guid id, AssignRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new AssignRiskCaseCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch), body.AnalystId), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> Start(Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new StartRiskCaseCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch)), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> AddNote(Guid id, NoteRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new AddCaseNoteCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch), body.Text), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> AddLink(Guid id, LinkRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new AddCaseLinkCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch), body.SourceNodeId, body.TargetNodeId, body.Kind, body.Rationale), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> Assess(Guid id, AssessmentRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new AssessRiskCaseCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch), body.Level, body.Rationale), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> RequestReview(Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new RequestCaseReviewCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch)), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> Resume(Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new ResumeRiskCaseCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch)), cancellationToken);
        return WithEtag(context, response);
    }

    public static async Task<IResult> Decide(Guid id, DecisionRequest body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var response = await sender.Send(new DecideRiskCaseCommand(idempotencyKey ?? "", id, ParseVersion(ifMatch), body.Disposition, body.Rationale), cancellationToken);
        return WithEtag(context, response);
    }

    private static int ParseVersion(string? value)
    {
        if (value is null || value.Length < 3 || value[0] != '"' || value[^1] != '"' ||
            !int.TryParse(value.AsSpan(1, value.Length - 2), out var version) || version < 1)
            throw new ApplicationFault(428, "if_match_required", "寫操作需要 If-Match: \"版本號\"。");
        return version;
    }

    private static IResult WithEtag(HttpContext context, RiskCaseView response)
    {
        context.Response.Headers.ETag = $"\"{response.Version}\"";
        return Results.Ok(response);
    }
}

public sealed record AssignRequest(string AnalystId);
public sealed record NoteRequest(string Text);
public sealed record LinkRequest(string SourceNodeId, string TargetNodeId, CaseLinkKind Kind, string Rationale);
public sealed record AssessmentRequest(RiskAssessmentLevel Level, string Rationale);
public sealed record DecisionRequest(RiskDisposition Disposition, string Rationale);
