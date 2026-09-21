using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;

namespace WalletLedger.Application.Ledger;

public sealed record GetWalletHistoryQuery(Guid OwnerUserId, Guid WalletId, int Page, int PageSize);

public sealed class GetWalletHistoryHandler(IWalletRepository walletRepository, ITransactionRepository transactionRepository)
{
    private const int MaxPageSize = 100;

    /// <summary>Same non-leak pattern as GetWalletByIdHandler: null for both "doesn't exist" and "not yours".</summary>
    public async Task<PagedResult<TransactionDto>?> HandleAsync(GetWalletHistoryQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
        {
            throw new ArgumentException("Page must be 1 or greater.", nameof(query));
        }

        if (query.PageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentException($"PageSize must be between 1 and {MaxPageSize}.", nameof(query));
        }

        var wallet = await walletRepository.GetByIdAsync(query.WalletId, ct);
        if (wallet is null || wallet.OwnerUserId != query.OwnerUserId)
        {
            return null;
        }

        var (items, totalCount) = await transactionRepository.ListTransactionsForAccountAsync(wallet.Id, query.Page, query.PageSize, ct);

        return new PagedResult<TransactionDto>(items.Select(t => t.ToDto()).ToList(), query.Page, query.PageSize, totalCount);
    }
}
