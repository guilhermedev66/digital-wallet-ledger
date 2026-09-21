using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Wallets;

/// <summary>OwnerUserId must be resolved server-side from the authenticated caller's claims — never from client input.</summary>
public sealed record CreateWalletCommand(Guid OwnerUserId, Currency Currency, string? DisplayName);

public sealed class CreateWalletHandler(IWalletRepository walletRepository)
{
    public async Task<WalletDto> HandleAsync(CreateWalletCommand command, CancellationToken ct)
    {
        var account = LedgerAccount.Open(command.OwnerUserId, command.Currency, command.DisplayName);

        await walletRepository.AddAsync(account, ct);

        return account.ToDto();
    }
}
