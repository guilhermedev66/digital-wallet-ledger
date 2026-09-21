using Microsoft.EntityFrameworkCore;
using Npgsql;
using WalletLedger.Application.Abstractions;
using WalletLedger.Application.Exceptions;
using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Infrastructure.Persistence;

public sealed class EfUserRepository(WalletLedgerDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByEmailAsync(Email email, CancellationToken ct) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct)
    {
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            // RegisterUserHandler's own GetByEmailAsync-then-insert check races against
            // concurrent registrations for the same email; the DB's unique index on Email
            // is the real guarantee, this just translates its violation into the same
            // exception the pre-check would throw, instead of letting a raw DbUpdateException
            // reach the API as an unhandled 500.
            throw new EmailAlreadyRegisteredException();
        }
    }

    private static bool IsUniqueEmailViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
