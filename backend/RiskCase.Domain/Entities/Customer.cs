using RiskCase.Domain.Common;

namespace RiskCase.Domain.Entities;

public sealed class Customer : BaseAuditableEntity
{
    public string CustomerNumber { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Segment { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string RiskTier { get; private set; } = string.Empty;

    private Customer() { }

    public static Customer Create(string number, string name, string segment, string countryCode, string riskTier)
    {
        if (string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("customer_profile_invalid", "客户编号与显示名称不能为空。");

        return new Customer
        {
            Id = Guid.NewGuid(),
            CustomerNumber = number.Trim(),
            DisplayName = name.Trim(),
            Segment = segment.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            RiskTier = riskTier.Trim()
        };
    }
}
