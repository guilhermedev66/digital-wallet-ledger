using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Infrastructure.Persistence.Configurations;

public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("LedgerAccounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.OwnerUserId);

        builder.HasIndex(a => a.OwnerUserId);

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.DisplayName)
            .HasMaxLength(200);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();
    }
}
