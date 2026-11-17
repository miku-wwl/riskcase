using System.Reflection;
using System.Data;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Entities;
using RiskCase.Infrastructure.Identity;
using RiskCaseEntity = RiskCase.Domain.Entities.RiskCase;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace RiskCase.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RiskAlert> RiskAlerts => Set<RiskAlert>();
    public DbSet<RiskSignal> RiskSignals => Set<RiskSignal>();
    public DbSet<RiskTransaction> RiskTransactions => Set<RiskTransaction>();
    public DbSet<RiskCaseEntity> RiskCases => Set<RiskCaseEntity>();
    public IQueryable<RiskCaseEntity> CaseDetails => RiskCases.AsSplitQuery();
    public DbSet<RiskCaseNote> RiskCaseNotes => Set<RiskCaseNote>();
    public DbSet<RiskCaseLink> RiskCaseLinks => Set<RiskCaseLink>();
    public DbSet<RiskDecision> RiskDecisions => Set<RiskDecision>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();
    public DbSet<LegacyAssessorParticipation> LegacyAssessorParticipation => Set<LegacyAssessorParticipation>();

    public async Task<T> WithCaseSnapshotAsync<T>(Guid caseId, Func<Task<T>> read, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is { } existing)
        {
            // All case writes update the parent before appending children/audit, in the same transaction.
            // Under READ COMMITTED a parent share lock stabilizes the following split reads, including
            // duplicate-ingestion reads. Reuse our own write lock; never start a nested transaction.
            if (existing.GetDbTransaction().IsolationLevel == IsolationLevel.ReadCommitted)
                await Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM riskcase.risk_cases WHERE \"Id\"={caseId} FOR SHARE", cancellationToken);
            else if (existing.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
                throw new InvalidOperationException("A case snapshot requires READ COMMITTED with a parent lock, or snapshot isolation.");
            return await read();
        }
        await using var snapshot = await Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var result = await read();
        await snapshot.CommitAsync(cancellationToken);
        return result;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries().Any(x => x.Entity is RiskAssessment or RiskDecision or AuditEvent or RiskCase.Domain.Entities.LegacyAssessorParticipation
            && x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Assessment, decision and audit records are append-only.");
        return base.SaveChangesAsync(cancellationToken);
    }
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IdempotencyReceipt> IdempotencyReceipts => Set<IdempotencyReceipt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("riskcase");
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
