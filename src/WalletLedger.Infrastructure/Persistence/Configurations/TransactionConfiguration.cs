using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.RequestedByUserId)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.PostedAtUtc)
            .IsRequired();

        builder.Property(t => t.ReversalOfTransactionId);

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
