using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    /// <summary>Shared with EfTransactionRepository, which matches on these names to tell unique-constraint violations apart by which index raised them.</summary>
    public const string IdempotencyKeyIndexName = "IX_Transactions_RequestedByUserId_IdempotencyKey";
    public const string ReversalOfTransactionIdIndexName = "IX_Transactions_ReversalOfTransactionId";

    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.RequestedByUserId)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        // The real guarantee behind every handler's idempotent-replay logic (see
        // SimulateFundingHandler/TransferHandler) - a check-then-insert in application code
        // always races against a concurrent identical request; this constraint is what makes
        // that race safe instead of a double-post. Named explicitly (rather than relying on
        // EF's default naming staying stable) since EfTransactionRepository matches on this
        // name to tell this violation apart from the reversal-uniqueness one below.
        builder.HasIndex(t => new { t.RequestedByUserId, t.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName(IdempotencyKeyIndexName);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.PostedAtUtc)
            .IsRequired();

        builder.Property(t => t.ReversalOfTransactionId);

        // A transaction can only be reversed once (see TransactionAlreadyReversedException) -
        // same TOCTOU shape as the idempotency constraint above: ReverseTransactionHandler's
        // own pre-check (FindReversalOfAsync) races against a concurrent reversal request under
        // a different idempotency key, so the DB constraint is the real guarantee. Filtered so
        // the many transactions that are never reversed (ReversalOfTransactionId IS NULL) don't
        // collide with each other.
        builder.HasIndex(t => t.ReversalOfTransactionId)
            .IsUnique()
            .HasFilter("\"ReversalOfTransactionId\" IS NOT NULL")
            .HasDatabaseName(ReversalOfTransactionIdIndexName);

        // Entries has no public setter/Add method (encapsulated collection - the only way to
        // populate it is Transaction.Post's balance-checked construction). EF Core materializes
        // it via the private `_entries` backing field instead of the property.
        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(t => t.Entries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
