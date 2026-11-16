using RiskCase.Domain.Entities;
using RiskCaseEntity = RiskCase.Domain.Entities.RiskCase;

namespace RiskCase.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Account> Accounts { get; }
    DbSet<RiskAlert> RiskAlerts { get; }
    DbSet<RiskSignal> RiskSignals { get; }
    DbSet<RiskTransaction> RiskTransactions { get; }
    DbSet<RiskCaseEntity> RiskCases { get; }
    DbSet<RiskCaseNote> RiskCaseNotes { get; }
    DbSet<RiskCaseLink> RiskCaseLinks { get; }
    DbSet<RiskDecision> RiskDecisions { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<IdempotencyReceipt> IdempotencyReceipts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
