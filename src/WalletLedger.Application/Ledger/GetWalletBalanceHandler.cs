using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;

namespace WalletLedger.Application.Ledger;

public sealed record GetWalletBalanceQuery(Guid OwnerUserId, Guid WalletId);

public sealed class GetWalletBalanceHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    /// <summary>Same non-leak pattern as GetWalletByIdHandler: null for both "doesn't exist" and "not yours".</summary>
    public async Task<WalletBalanceDto?> HandleAsync(GetWalletBalanceQuery query, CancellationToken ct)
    {
        var wallet = await walletRepository.GetByIdAsync(query.WalletId, ct);
        if (wallet is null || wallet.OwnerUserId != query.OwnerUserId)
        {
            return null;
        }

        var balance = await transactionRepository.GetAccountBalanceAsync(wallet.Id, ct);

        return new WalletBalanceDto(wallet.Id, wallet.Currency.ToString(), balance);
    }
}
