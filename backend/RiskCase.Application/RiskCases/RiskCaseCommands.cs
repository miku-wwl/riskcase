using System.Text.Json;
using RiskCase.Application.Common.Behaviours;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Application.Common.Security;
using RiskCase.Domain.Common;
using RiskCase.Domain.Constants;
using RiskCase.Domain.Entities;
using RiskCase.Domain.Enums;

namespace RiskCase.Application.RiskCases;

public sealed record IngestAlertCommand(
    string IdempotencyKey,
    string ExternalAlertId,
    string SourceSystem,
    RiskAlertCategory Category,
    DateTimeOffset DetectedAt,
    RiskSeverity Severity,
    int? RiskScore,
    Guid CustomerId,
    Guid AccountId,
    IReadOnlyList<RiskSignalInput> Signals,
    IReadOnlyList<RiskTransactionInput> Transactions) : IRequest<RiskCaseView>, IIdempotentRequest;

public sealed class RiskSignalInputValidator : AbstractValidator<RiskSignalInput>
{
    public RiskSignalInputValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Explanation).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.TransactionReferences).NotNull();
    }
}

public sealed class RiskTransactionInputValidator : AbstractValidator<RiskTransactionInput>
{
    public RiskTransactionInputValidator()
    {
        RuleFor(x => x.ExternalReference).NotEmpty().MaximumLength(128);
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.OccurredAt).NotEmpty();
        RuleFor(x => x.Channel).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CounterpartyReference).NotEmpty().MaximumLength(128);
        RuleFor(x => x.CounterpartyLabel).NotEmpty().MaximumLength(160);
    }
}

public sealed class IngestAlertCommandValidator : AbstractValidator<IngestAlertCommand>
{
    public IngestAlertCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.ExternalAlertId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.SourceSystem).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.RiskScore).InclusiveBetween(0, 100).When(x => x.RiskScore.HasValue);
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Signals).NotEmpty();
        RuleFor(x => x.Transactions).NotEmpty();
        RuleForEach(x => x.Signals).SetValidator(new RiskSignalInputValidator());
        RuleForEach(x => x.Transactions).SetValidator(new RiskTransactionInputValidator());
    }
}

public sealed class IngestAlertCommandHandler(
    IApplicationDbContext db,
    IAdvisoryLockProvider locks,
    TimeProvider clock)
    : IRequestHandler<IngestAlertCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(IngestAlertCommand request, CancellationToken cancellationToken)
    {
        await locks.AcquireAsync($"alert:{request.SourceSystem}:{request.ExternalAlertId}", cancellationToken);

        var fingerprint = IdempotencyBehaviour<IngestAlertCommand, RiskCaseView>.Fingerprint(request);
        var existing = await db.RiskAlerts.AsNoTracking()
            .Where(x => x.SourceSystem == request.SourceSystem && x.ExternalAlertId == request.ExternalAlertId)
            .Select(x => new { x.Id, x.IngestionFingerprint })
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (!StringComparer.Ordinal.Equals(existing.IngestionFingerprint, fingerprint))
                throw new ApplicationFault(409, "external_alert_payload_conflict", "同一来源告警编号已存在，但载荷内容不同。");
            var existingCaseId = await db.RiskCases.AsNoTracking().Where(x => x.AlertId == existing.Id).Select(x => x.Id).SingleAsync(cancellationToken);
            return await RiskCasePresenter.LoadAsync(db, existingCaseId, cancellationToken);
        }

        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken);
        if (customer is null || account is null || account.CustomerId != request.CustomerId)
            throw new ApplicationFault(400, "alert_subject_invalid", "告警关联的客户或账户不存在，或账户不属于该客户。");

        var transactionReferences = request.Transactions.Select(x => x.ExternalReference).ToHashSet(StringComparer.Ordinal);
        if (request.Signals.SelectMany(x => x.TransactionReferences).Any(x => !transactionReferences.Contains(x)))
            throw new ApplicationFault(400, "signal_transaction_reference_invalid", "风险信号引用了当前告警载荷中不存在的交易。");

        var accountIds = request.Transactions.Select(x => x.AccountId).Append(request.AccountId).Distinct().ToArray();
        var knownAccountIds = await db.Accounts.AsNoTracking().Where(x => accountIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(cancellationToken);
        if (knownAccountIds.Count != accountIds.Length)
            throw new ApplicationFault(400, "transaction_account_invalid", "交易引用了不存在的账户。");

        var alertId = Guid.NewGuid();
        var signals = request.Signals.Select(x => RiskSignal.Explain(alertId, x.Code, x.Title, x.Explanation, x.Severity, x.TransactionReferences)).ToArray();
        var alert = RiskAlert.Receive(alertId, request.ExternalAlertId, request.SourceSystem, fingerprint, request.Category,
            request.DetectedAt, request.Severity, request.RiskScore, request.CustomerId, request.AccountId, signals);
        var riskCase = RiskCase.Domain.Entities.RiskCase.Open(alertId);
        foreach (var transaction in request.Transactions)
            db.RiskTransactions.Add(RiskTransaction.Record(alertId, transaction.AccountId, transaction.ExternalReference,
                transaction.Direction, transaction.Amount, transaction.Currency, transaction.OccurredAt, transaction.Channel,
                transaction.CounterpartyReference, transaction.CounterpartyLabel));

        db.RiskAlerts.Add(alert);
        db.RiskCases.Add(riskCase);
        db.AuditEvents.Add(AuditEvent.Record(riskCase.Id, riskCase.Version, $"upstream:{request.SourceSystem}", "AlertIngested",
            JsonSerializer.Serialize(new { request.ExternalAlertId, request.SourceSystem, transactionCount = request.Transactions.Count }), clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
        return await RiskCasePresenter.LoadAsync(db, riskCase.Id, cancellationToken);
    }
}

public interface IVersionedRiskCaseCommand : IIdempotentRequest
{
    Guid CaseId { get; }
    int ExpectedVersion { get; }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record AssignRiskCaseCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, string AnalystId)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record StartRiskCaseCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record AddCaseNoteCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, string Text)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record AddCaseLinkCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, string SourceNodeId, string TargetNodeId, CaseLinkKind Kind, string Rationale)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record AssessRiskCaseCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, RiskAssessmentLevel Level, string Rationale)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record RequestCaseReviewCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.CaseInvestigators)]
public sealed record ResumeRiskCaseCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;
[Authorize(Roles = Roles.Supervisor)]
public sealed record DecideRiskCaseCommand(string IdempotencyKey, Guid CaseId, int ExpectedVersion, RiskDisposition Disposition, string Rationale)
    : IRequest<RiskCaseView>, IVersionedRiskCaseCommand;

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AssignRiskCaseCommandValidator : AbstractValidator<AssignRiskCaseCommand>
{
    public AssignRiskCaseCommandValidator() { RuleFor(x => x.AnalystId).NotEmpty().MaximumLength(450); RuleFor(x => x.ExpectedVersion).GreaterThan(0); }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class StartRiskCaseCommandValidator : AbstractValidator<StartRiskCaseCommand>
{
    public StartRiskCaseCommandValidator() => RuleFor(x => x.ExpectedVersion).GreaterThan(0);
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AddCaseNoteCommandValidator : AbstractValidator<AddCaseNoteCommand>
{
    public AddCaseNoteCommandValidator() { RuleFor(x => x.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Text).NotEmpty().MaximumLength(2000); }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AddCaseLinkCommandValidator : AbstractValidator<AddCaseLinkCommand>
{
    public AddCaseLinkCommandValidator() { RuleFor(x => x.ExpectedVersion).GreaterThan(0); RuleFor(x => x.SourceNodeId).NotEmpty().MaximumLength(180); RuleFor(x => x.TargetNodeId).NotEmpty().MaximumLength(180); RuleFor(x => x.Kind).IsInEnum(); RuleFor(x => x.Rationale).NotEmpty().MaximumLength(500); }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AssessRiskCaseCommandValidator : AbstractValidator<AssessRiskCaseCommand>
{
    public AssessRiskCaseCommandValidator() { RuleFor(x => x.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Level).IsInEnum(); RuleFor(x => x.Rationale).NotEmpty().MaximumLength(1500); }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class RequestCaseReviewCommandValidator : AbstractValidator<RequestCaseReviewCommand>
{
    public RequestCaseReviewCommandValidator() => RuleFor(x => x.ExpectedVersion).GreaterThan(0);
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class ResumeRiskCaseCommandValidator : AbstractValidator<ResumeRiskCaseCommand>
{
    public ResumeRiskCaseCommandValidator() => RuleFor(x => x.ExpectedVersion).GreaterThan(0);
}

[Authorize(Roles = Roles.Supervisor)]
public sealed class DecideRiskCaseCommandValidator : AbstractValidator<DecideRiskCaseCommand>
{
    public DecideRiskCaseCommandValidator() { RuleFor(x => x.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Disposition).IsInEnum(); RuleFor(x => x.Rationale).NotEmpty().MaximumLength(2000); }
}

public abstract class RiskCaseCommandHandlerBase(IApplicationDbContext db, IUser user, TimeProvider clock)
{
    protected IApplicationDbContext Db { get; } = db;
    protected TimeProvider Clock { get; } = clock;
    private IUser User { get; } = user;

    protected async Task<RiskCase.Domain.Entities.RiskCase> LoadForWriteAsync(IVersionedRiskCaseCommand request, CancellationToken cancellationToken)
    {
        var entity = await Db.RiskCases.SingleOrDefaultAsync(x => x.Id == request.CaseId, cancellationToken)
            ?? throw new ApplicationFault(404, "risk_case_not_found", "风控档案不存在。");
        if (entity.Version != request.ExpectedVersion)
            throw new ApplicationFault(412, "version_conflict", $"档案版本已从 {request.ExpectedVersion} 更新为 {entity.Version}。请刷新后重新确认操作。");
        return entity;
    }

    protected string AnalystId => User.Id ?? throw new UnauthorizedAccessException();
    protected bool IsSupervisor => User.Roles?.Contains(Roles.Supervisor, StringComparer.Ordinal) == true;

    protected async Task<RiskCaseView> SaveAsync(RiskCase.Domain.Entities.RiskCase entity, string operation, object details, CancellationToken cancellationToken)
    {
        Db.AuditEvents.Add(AuditEvent.Record(entity.Id, entity.Version, AnalystId, operation, JsonSerializer.Serialize(details), Clock.GetUtcNow()));
        await Db.SaveChangesAsync(cancellationToken);
        return await RiskCasePresenter.LoadAsync(Db, entity.Id, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AssignRiskCaseCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<AssignRiskCaseCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(AssignRiskCaseCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        if (!IsSupervisor && (!StringComparer.Ordinal.Equals(request.AnalystId, AnalystId) || (entity.AssignedAnalystId is not null && entity.AssignedAnalystId != AnalystId)))
            throw new ForbiddenAccessException();
        entity.Assign(request.AnalystId, Clock.GetUtcNow());
        return await SaveAsync(entity, "AnalystAssigned", new { request.AnalystId }, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class StartRiskCaseCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<StartRiskCaseCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(StartRiskCaseCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        entity.StartInvestigation(AnalystId, Clock.GetUtcNow());
        return await SaveAsync(entity, "InvestigationStarted", new { }, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AddCaseNoteCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<AddCaseNoteCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(AddCaseNoteCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        entity.AddNote(AnalystId, request.Text, Clock.GetUtcNow());
        return await SaveAsync(entity, "NoteAdded", new { textLength = request.Text.Trim().Length }, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AddCaseLinkCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<AddCaseLinkCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(AddCaseLinkCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        var view = await RiskCasePresenter.LoadAsync(Db, entity.Id, cancellationToken);
        var nodeIds = view.GraphNodes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        if (!nodeIds.Contains(request.SourceNodeId) || !nodeIds.Contains(request.TargetNodeId))
            throw new ApplicationFault(400, "case_link_endpoint_not_found", "关系端点必须来自当前档案的调查图谱。");
        entity.AddLink(AnalystId, request.SourceNodeId, request.TargetNodeId, request.Kind, request.Rationale, Clock.GetUtcNow());
        return await SaveAsync(entity, "RelationshipRecorded", new { request.SourceNodeId, request.TargetNodeId, request.Kind }, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class AssessRiskCaseCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<AssessRiskCaseCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(AssessRiskCaseCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        entity.RecordAssessment(AnalystId, request.Level, request.Rationale, Clock.GetUtcNow());
        return await SaveAsync(entity, "AssessmentRecorded", new { request.Level }, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class RequestCaseReviewCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<RequestCaseReviewCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(RequestCaseReviewCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        entity.RequestReview(AnalystId, Clock.GetUtcNow());
        return await SaveAsync(entity, "ReviewRequested", new { }, cancellationToken);
    }
}

[Authorize(Roles = Roles.CaseInvestigators)]
public sealed class ResumeRiskCaseCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<ResumeRiskCaseCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(ResumeRiskCaseCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        entity.ResumeInvestigation(AnalystId, Clock.GetUtcNow());
        return await SaveAsync(entity, "InvestigationResumed", new { }, cancellationToken);
    }
}

[Authorize(Roles = Roles.Supervisor)]
public sealed class DecideRiskCaseCommandHandler(IApplicationDbContext db, IUser user, TimeProvider clock)
    : RiskCaseCommandHandlerBase(db, user, clock), IRequestHandler<DecideRiskCaseCommand, RiskCaseView>
{
    public async Task<RiskCaseView> Handle(DecideRiskCaseCommand request, CancellationToken cancellationToken)
    {
        var entity = await LoadForWriteAsync(request, cancellationToken);
        entity.Decide(AnalystId, request.Disposition, request.Rationale, Clock.GetUtcNow());
        return await SaveAsync(entity, "DispositionRecorded", new { request.Disposition }, cancellationToken);
    }
}
