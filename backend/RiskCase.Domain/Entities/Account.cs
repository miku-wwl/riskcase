using RiskCase.Domain.Common;

namespace RiskCase.Domain.Entities;

public sealed class Account : BaseAuditableEntity
{
    public Guid CustomerId { get; private set; }
    public string MaskedNumber { get; private set; } = string.Empty;
    public string AccountType { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateOnly OpenedOn { get; private set; }
    public Customer Customer { get; private set; } = null!;

    private Account() { }

    public static Account Create(Guid customerId, string maskedNumber, string accountType, string currency, DateOnly openedOn)
    {
        if (customerId == Guid.Empty || string.IsNullOrWhiteSpace(maskedNumber) || maskedNumber.Length > 32)
            throw new DomainRuleException("account_profile_invalid", "账户资料无效。");

        return new Account
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            MaskedNumber = maskedNumber.Trim(),
            AccountType = accountType.Trim(),
            Currency = currency.Trim().ToUpperInvariant(),
            Status = "Open",
            OpenedOn = openedOn
        };
    }
}
