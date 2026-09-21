using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(Email email, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);
}
