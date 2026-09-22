using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Dtos;
using WalletLedger.Application.Mapping;
using WalletLedger.Domain.Entities;

namespace WalletLedger.Application.Ledger;

/// <summary>FromUtc/ToUtc/Type are all optional filters on top of the same page-based pagination convention M3 established - not switched to cursor-based.</summary>
public sealed record GetWalletHistoryQuery(
    Guid OwnerUserId, Guid WalletId, int Page, int PageSize, DateTime? FromUtc = null, DateTime? ToUtc = null, TransactionType? Type = null);

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

        if (query.FromUtc is not null && query.ToUtc is not null && query.FromUtc > query.ToUtc)
        {
            throw new ArgumentException("FromUtc must not be later than ToUtc.", nameof(query));
        }

        var wallet = await walletRepository.GetByIdAsync(query.WalletId, ct);
        if (wallet is null || wallet.OwnerUserId != query.OwnerUserId)
        {
            return null;
        }

        var (items, totalCount) = await transactionRepository.ListTransactionsForAccountAsync(
            wallet.Id, query.Page, query.PageSize, query.FromUtc, query.ToUtc, query.Type, ct);

        return new PagedResult<TransactionDto>(items.Select(t => t.ToDto()).ToList(), query.Page, query.PageSize, totalCount);
    }
}
