using RiskCase.Domain.Entities;
using RiskCase.Domain.Enums;

namespace RiskCase.Application.RiskCases;

public sealed record CustomerView(Guid Id, string CustomerNumber, string DisplayName, string Segment, string CountryCode, string RiskTier);
public sealed record AccountView(Guid Id, string MaskedNumber, string AccountType, string Currency, string Status, DateOnly OpenedOn);
public sealed record SignalView(string Code, string Title, string Explanation, RiskSeverity Severity, string[] TransactionReferences);
public sealed record TransactionView(Guid Id, string ExternalReference, TransactionDirection Direction, decimal Amount, string Currency, DateTimeOffset OccurredAt, string Channel, string CounterpartyReference, string CounterpartyLabel, string AccountMaskedNumber);
public sealed record NoteView(Guid Id, string AnalystId, string Text, DateTimeOffset OccurredAt, int CaseVersion);
public sealed record LinkView(Guid Id, string SourceNodeId, string TargetNodeId, CaseLinkKind Kind, string Rationale, string AnalystId, DateTimeOffset OccurredAt, int CaseVersion);
public sealed record DecisionView(Guid Id, RiskDisposition Disposition, string Rationale, string ReviewerId, DateTimeOffset DecidedAt, int CaseVersion);
public sealed record AuditView(int Version, string Operation, string ActorId, DateTimeOffset OccurredAt, string DetailsJson);
public sealed record GraphNodeView(string Id, string Type, string Label, string Detail, bool IsAlertSubject);
public sealed record GraphEdgeView(string Id, string Source, string Target, string Label, string? Rationale);
public sealed record AlertView(Guid Id, string ExternalAlertId, string SourceSystem, RiskAlertCategory Category, DateTimeOffset DetectedAt, RiskSeverity Severity, int? RiskScore);

public sealed record RiskCaseQueueItem(
    Guid Id,
    int Version,
    RiskCaseStatus Status,
    string? AssignedAnalystId,
    DateTimeOffset Created,
    DateTimeOffset LastModified,
    AlertView Alert,
    string CustomerName,
    string CustomerNumber,
    string AccountMaskedNumber,
    int SignalCount,
    string SignalSummary);

public sealed record RiskCaseQueuePage(
    IReadOnlyList<RiskCaseQueueItem> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record RiskCaseView(
    Guid Id,
    int Version,
    RiskCaseStatus Status,
    string? AssignedAnalystId,
    DateTimeOffset Created,
    DateTimeOffset LastModified,
    string? AssessmentLevel,
    string? AssessmentRationale,
    DateTimeOffset? AssessedAt,
    AlertView Alert,
    CustomerView Customer,
    AccountView Account,
    IReadOnlyList<SignalView> Signals,
    IReadOnlyList<TransactionView> Transactions,
    IReadOnlyList<NoteView> Notes,
    IReadOnlyList<LinkView> Links,
    IReadOnlyList<DecisionView> Decisions,
    IReadOnlyList<AuditView> AuditHistory,
    IReadOnlyList<GraphNodeView> GraphNodes,
    IReadOnlyList<GraphEdgeView> GraphEdges);

public sealed record RiskSignalInput(string Code, string Title, string Explanation, RiskSeverity Severity, string[] TransactionReferences);
public sealed record RiskTransactionInput(string ExternalReference, Guid AccountId, TransactionDirection Direction, decimal Amount, string Currency, DateTimeOffset OccurredAt, string Channel, string CounterpartyReference, string CounterpartyLabel);
