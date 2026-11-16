using System.Reflection;
using RiskCase.Application.Common.Interfaces;
using RiskCase.Domain.Entities;
using RiskCase.Infrastructure.Identity;
using RiskCaseEntity = RiskCase.Domain.Entities.RiskCase;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<RiskCaseNote> RiskCaseNotes => Set<RiskCaseNote>();
    public DbSet<RiskCaseLink> RiskCaseLinks => Set<RiskCaseLink>();
    public DbSet<RiskDecision> RiskDecisions => Set<RiskDecision>();
    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries().Any(x => x.Entity is RiskAssessment or RiskDecision or AuditEvent
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
