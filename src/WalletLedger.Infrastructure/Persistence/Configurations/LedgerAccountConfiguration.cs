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

        // Closes the same TOCTOU shape already fixed once for User.Email in M1: the DB is
        // the real guarantee against two near-simultaneous startups both seeding a
        // SystemFunding account for the same currency, not just the check-then-create in
        // Program.cs's seeding routine. Only SystemFunding rows participate - many
        // UserWallet rows legitimately share a (Type, Currency) pair.
        builder.HasIndex(a => new { a.Type, a.Currency })
            .IsUnique()
            .HasFilter("\"Type\" = 'SystemFunding'");

        builder.Property(a => a.DisplayName)
            .HasMaxLength(200);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();
    }
}
