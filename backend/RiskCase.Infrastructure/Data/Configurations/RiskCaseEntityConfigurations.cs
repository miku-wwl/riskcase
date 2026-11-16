using RiskCase.Domain.Entities;
using RiskCaseEntity = RiskCase.Domain.Entities.RiskCase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RiskCase.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.CustomerNumber).HasMaxLength(64).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        b.Property(x => x.Segment).HasMaxLength(64).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(x => x.RiskTier).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.CustomerNumber).IsUnique();
    }
}

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.MaskedNumber).HasMaxLength(32).IsRequired();
        b.Property(x => x.AccountType).HasMaxLength(48).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MaskedNumber).IsUnique();
        b.HasIndex(x => x.CustomerId);
    }
}

public sealed class RiskAlertConfiguration : IEntityTypeConfiguration<RiskAlert>
{
    public void Configure(EntityTypeBuilder<RiskAlert> b)
    {
        b.ToTable("risk_alerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalAlertId).HasMaxLength(128).IsRequired();
        b.Property(x => x.SourceSystem).HasMaxLength(80).IsRequired();
        b.Property(x => x.IngestionFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Signals).WithOne(x => x.Alert).HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Signals).HasField("_signals").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Transactions).WithOne(x => x.Alert).HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.SourceSystem, x.ExternalAlertId }).IsUnique();
        b.HasIndex(x => new { x.DetectedAt, x.Severity });
    }
}

public sealed class RiskSignalConfiguration : IEntityTypeConfiguration<RiskSignal>
{
    public void Configure(EntityTypeBuilder<RiskSignal> b)
    {
        b.ToTable("risk_signals");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Title).HasMaxLength(160).IsRequired();
        b.Property(x => x.Explanation).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.TransactionReferences).HasColumnType("text[]");
        b.HasIndex(x => new { x.AlertId, x.Code });
    }
}

public sealed class RiskTransactionConfiguration : IEntityTypeConfiguration<RiskTransaction>
{
    public void Configure(EntityTypeBuilder<RiskTransaction> b)
    {
        b.ToTable("risk_transactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.ExternalReference).HasMaxLength(128).IsRequired();
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(64).IsRequired();
        b.Property(x => x.CounterpartyReference).HasMaxLength(128).IsRequired();
        b.Property(x => x.CounterpartyLabel).HasMaxLength(160).IsRequired();
        b.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.AlertId, x.ExternalReference }).IsUnique();
        b.HasIndex(x => x.OccurredAt);
    }
}

public sealed class RiskCaseConfiguration : IEntityTypeConfiguration<RiskCaseEntity>
{
    public void Configure(EntityTypeBuilder<RiskCaseEntity> b)
    {
        b.ToTable("risk_cases");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.AssignedAnalystId).HasMaxLength(450);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.AssessmentLevel).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.AssessmentRationale).HasMaxLength(1500);
        b.Property(x => x.AssessedById).HasMaxLength(450);
        b.Property(x => x.ReviewSubmittedById).HasMaxLength(450);
        b.HasMany(x => x.Assessments).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Assessments).HasField("_assessments").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasOne(x => x.Alert).WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.AlertId).IsUnique();
        b.HasIndex(x => new { x.Status, x.AssignedAnalystId, x.LastModified });
        b.HasMany(x => x.Notes).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Notes).HasField("_notes").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Links).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Links).HasField("_links").UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasMany(x => x.Decisions).WithOne(x => x.Case).HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(x => x.Decisions).HasField("_decisions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class RiskCaseNoteConfiguration : IEntityTypeConfiguration<RiskCaseNote>
{
    public void Configure(EntityTypeBuilder<RiskCaseNote> b)
    {
        b.ToTable("risk_case_notes");
        b.HasKey(x => x.Id);
        b.Property(x => x.AnalystId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.CaseId, x.OccurredAt });
    }
}

public sealed class RiskCaseLinkConfiguration : IEntityTypeConfiguration<RiskCaseLink>
{
    public void Configure(EntityTypeBuilder<RiskCaseLink> b)
    {
        b.ToTable("risk_case_links", table => table.HasCheckConstraint("ck_risk_case_links_distinct_nodes", "\"SourceNodeId\" <> \"TargetNodeId\""));
        b.HasKey(x => x.Id);
        b.Property(x => x.SourceNodeId).HasMaxLength(180).IsRequired();
        b.Property(x => x.TargetNodeId).HasMaxLength(180).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Rationale).HasMaxLength(500).IsRequired();
        b.Property(x => x.AnalystId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.CaseId, x.SourceNodeId, x.TargetNodeId }).IsUnique();
    }
}

public sealed class RiskDecisionConfiguration : IEntityTypeConfiguration<RiskDecision>
{
    public void Configure(EntityTypeBuilder<RiskDecision> b)
    {
        b.ToTable("risk_decisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Disposition).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Rationale).HasMaxLength(2000).IsRequired();
        b.Property(x => x.AnalystId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.CaseId, x.CaseVersion }).IsUnique();
        b.HasOne<RiskAssessment>().WithMany().HasForeignKey(x => new { x.CaseId, x.AssessmentId })
            .HasPrincipalKey(x => new { x.CaseId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RiskAssessmentConfiguration : IEntityTypeConfiguration<RiskAssessment>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> b)
    {
        b.ToTable("risk_assessments", t => t.HasCheckConstraint("ck_assessment_author", "\"IsLegacy\" OR \"AssessorId\" IS NOT NULL"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.CaseId, x.Id });
        b.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Rationale).HasMaxLength(1500).IsRequired();
        b.Property(x => x.AssessorId).HasMaxLength(450);
        b.HasIndex(x => new { x.CaseId, x.CaseVersion }).IsUnique();
    }
}

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Operation).HasMaxLength(64).IsRequired();
        b.Property(x => x.DetailsJson).HasColumnType("jsonb").IsRequired();
        b.HasOne(x => x.Case).WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CaseId, x.CaseVersion }).IsUnique();
    }
}

public sealed class IdempotencyReceiptConfiguration : IEntityTypeConfiguration<IdempotencyReceipt>
{
    public void Configure(EntityTypeBuilder<IdempotencyReceipt> b)
    {
        b.ToTable("idempotency_receipts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Scope).HasMaxLength(180).IsRequired();
        b.Property(x => x.Key).HasMaxLength(36).IsRequired();
        b.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.ResponseType).HasMaxLength(500).IsRequired();
        b.Property(x => x.ResponseJson).HasColumnType("jsonb").IsRequired();
        b.HasIndex(x => new { x.Scope, x.Key }).IsUnique();
        b.HasIndex(x => x.CreatedAt);
    }
}
