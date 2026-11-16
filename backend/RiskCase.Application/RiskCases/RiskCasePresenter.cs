using System.Text.Json;
using RiskCase.Application.Common.Exceptions;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Entities;
using RiskCase.Domain.Enums;
using RiskCaseEntity = RiskCase.Domain.Entities.RiskCase;
using Microsoft.EntityFrameworkCore;

namespace RiskCase.Application.RiskCases;

public static class RiskCasePresenter
{
    public static async Task<RiskCaseView> LoadAsync(IApplicationDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var entity = await db.RiskCases
            .AsNoTracking()
            .Include(x => x.Alert).ThenInclude(x => x.Customer)
            .Include(x => x.Alert).ThenInclude(x => x.Account)
            .Include(x => x.Alert).ThenInclude(x => x.Signals)
            .Include(x => x.Alert).ThenInclude(x => x.Transactions).ThenInclude(x => x.Account).ThenInclude(x => x.Customer)
            .Include(x => x.Notes)
            .Include(x => x.Links)
            .Include(x => x.Decisions)
            .Include(x => x.Assessments)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            throw new ApplicationFault(404, "risk_case_not_found", "风控档案不存在。");

        var audit = await db.AuditEvents.AsNoTracking()
            .Where(x => x.CaseId == id)
            .OrderBy(x => x.CaseVersion)
            .Select(x => new AuditView(x.CaseVersion, x.Operation, x.ActorId, x.OccurredAt, x.DetailsJson))
            .ToListAsync(cancellationToken);

        return Map(entity, audit);
    }

    public static RiskCaseView Map(RiskCaseEntity entity, IReadOnlyList<AuditView>? audit = null)
    {
        var alert = entity.Alert;
        var customer = alert.Customer;
        var account = alert.Account;
        var transactions = alert.Transactions.OrderBy(x => x.OccurredAt).ToArray();

        var nodes = new Dictionary<string, GraphNodeView>(StringComparer.Ordinal)
        {
            [$"customer:{customer.Id}"] = new($"customer:{customer.Id}", "customer", customer.DisplayName, $"客户号 {customer.CustomerNumber} · {customer.CountryCode} · 风险层级 {customer.RiskTier}", true),
            [$"account:{account.Id}"] = new($"account:{account.Id}", "account", account.MaskedNumber, $"{account.AccountType} · {account.Currency} · {account.Status}", true)
        };
        var edges = new List<GraphEdgeView>
        {
            new($"owns:{customer.Id}:{account.Id}", $"customer:{customer.Id}", $"account:{account.Id}", "持有账户", null)
        };

        foreach (var transaction in transactions)
        {
            var transactionNode = $"transaction:{transaction.Id}";
            var ownerNode = $"account:{transaction.AccountId}";
            if (!nodes.ContainsKey(ownerNode))
                nodes[ownerNode] = new(ownerNode, "account", transaction.Account.MaskedNumber, $"{transaction.Account.AccountType} · {transaction.Account.Currency}", false);
            var ownerCustomer = transaction.Account.Customer;
            var ownerCustomerNode = $"customer:{ownerCustomer.Id}";
            nodes.TryAdd(ownerCustomerNode, new(ownerCustomerNode, "customer", ownerCustomer.DisplayName, $"客户号 {ownerCustomer.CustomerNumber} · {ownerCustomer.CountryCode} · 风险层级 {ownerCustomer.RiskTier}", false));
            if (!edges.Any(x => x.Source == ownerCustomerNode && x.Target == ownerNode))
                edges.Add(new($"owns:{ownerCustomer.Id}:{transaction.Account.Id}", ownerCustomerNode, ownerNode, "持有账户", null));
            nodes[transactionNode] = new(transactionNode, "transaction", $"{transaction.Direction} {transaction.Amount:N2} {transaction.Currency}", $"{transaction.OccurredAt:yyyy-MM-dd HH:mm} · {transaction.Channel} · {transaction.ExternalReference}", false);
            edges.Add(new($"activity:{transaction.Id}", ownerNode, transactionNode, transaction.Direction == TransactionDirection.Outgoing ? "转出" : "转入", null));

            var counterpartyNode = $"counterparty:{transaction.CounterpartyReference}";
            nodes.TryAdd(counterpartyNode, new(counterpartyNode, "counterparty", transaction.CounterpartyLabel, transaction.CounterpartyReference, false));
            edges.Add(new($"counterparty:{transaction.Id}", transactionNode, counterpartyNode, "交易对手", null));
        }

        edges.AddRange(entity.Links.OrderBy(x => x.OccurredAt).Select(x => new GraphEdgeView(x.Id.ToString(), x.SourceNodeId, x.TargetNodeId, x.Kind.ToString(), x.Rationale)));

        return new RiskCaseView(
            entity.Id, entity.Version, entity.Status, entity.AssignedAnalystId, entity.Created, entity.LastModified,
            entity.AssessmentLevel is { } level ? JsonNamingPolicy.CamelCase.ConvertName(level.ToString()) : null,
            entity.AssessmentRationale, entity.AssessedAt, entity.AssessedById,
            entity.CurrentAssessmentId, entity.ReviewedAssessmentId, entity.ReviewSubmittedById, entity.ReviewSubmittedAt,
            entity.Assessments.OrderBy(x => x.CaseVersion).Select(x => new AssessmentView(x.Id, x.Level, x.Rationale, x.AssessorId, x.AssessedAt, x.CaseVersion, x.BasisVersion, x.IsLegacy)).ToArray(),
            new(alert.Id, alert.ExternalAlertId, alert.SourceSystem, alert.Category, alert.DetectedAt, alert.Severity, alert.RiskScore),
            new(customer.Id, customer.CustomerNumber, customer.DisplayName, customer.Segment, customer.CountryCode, customer.RiskTier),
            new(account.Id, account.MaskedNumber, account.AccountType, account.Currency, account.Status, account.OpenedOn),
            alert.Signals.OrderByDescending(x => x.Severity).Select(x => new SignalView(x.Code, x.Title, x.Explanation, x.Severity, x.TransactionReferences)).ToArray(),
            transactions.Select(x => new TransactionView(x.Id, x.ExternalReference, x.Direction, x.Amount, x.Currency, x.OccurredAt, x.Channel, x.CounterpartyReference, x.CounterpartyLabel, x.Account.MaskedNumber)).ToArray(),
            entity.Notes.OrderBy(x => x.OccurredAt).Select(x => new NoteView(x.Id, x.AnalystId, x.Text, x.OccurredAt, x.CaseVersion)).ToArray(),
            entity.Links.OrderBy(x => x.OccurredAt).Select(x => new LinkView(x.Id, x.SourceNodeId, x.TargetNodeId, x.Kind, x.Rationale, x.AnalystId, x.OccurredAt, x.CaseVersion)).ToArray(),
            entity.Decisions.OrderBy(x => x.DecidedAt).Select(x => new DecisionView(x.Id, x.Disposition, x.Rationale, x.AnalystId, x.DecidedAt, x.CaseVersion, x.AssessmentId)).ToArray(),
            audit ?? [], nodes.Values.ToArray(), edges.ToArray());
    }
}
