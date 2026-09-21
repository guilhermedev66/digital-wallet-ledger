using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TransactionId)
            .IsRequired();

        builder.Property(e => e.AccountId)
            .IsRequired();

        // Hot path for balance queries (ITransactionRepository.GetAccountBalanceAsync filters by this).
        builder.HasIndex(e => e.AccountId);

        builder.Property(e => e.Direction)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.AmountMinorUnits)
            .IsRequired();

        builder.Property(e => e.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();
    }
}
