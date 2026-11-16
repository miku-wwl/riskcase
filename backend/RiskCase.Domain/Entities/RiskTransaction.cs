using RiskCase.Domain.Common;
using RiskCase.Domain.Enums;

namespace RiskCase.Domain.Entities;

public sealed class RiskTransaction : BaseAuditableEntity
{
    public Guid AlertId { get; private set; }
    public Guid AccountId { get; private set; }
    public string ExternalReference { get; private set; } = string.Empty;
    public TransactionDirection Direction { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string CounterpartyReference { get; private set; } = string.Empty;
    public string CounterpartyLabel { get; private set; } = string.Empty;
    public RiskAlert Alert { get; private set; } = null!;
    public Account Account { get; private set; } = null!;

    private RiskTransaction() { }

    public static RiskTransaction Record(
        Guid alertId,
        Guid accountId,
        string externalReference,
        TransactionDirection direction,
        decimal amount,
        string currency,
        DateTimeOffset occurredAt,
        string channel,
        string counterpartyReference,
        string counterpartyLabel)
    {
        if (alertId == Guid.Empty || accountId == Guid.Empty || string.IsNullOrWhiteSpace(externalReference))
            throw new DomainRuleException("transaction_reference_invalid", "交易关联资料无效。");
        if (amount <= 0)
            throw new DomainRuleException("transaction_amount_invalid", "调查记录中的交易金额必须为正数。");

        return new RiskTransaction
        {
            Id = Guid.NewGuid(),
            AlertId = alertId,
            AccountId = accountId,
            ExternalReference = externalReference.Trim(),
            Direction = direction,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            OccurredAt = occurredAt,
            Channel = channel.Trim(),
            CounterpartyReference = counterpartyReference.Trim(),
            CounterpartyLabel = counterpartyLabel.Trim()
        };
    }
}
