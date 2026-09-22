namespace WalletLedger.Application.Exceptions;

/// <summary>Email already has an account. Mapped to 409 Conflict at the API boundary.</summary>
public sealed class EmailAlreadyRegisteredException : Exception
{
    public EmailAlreadyRegisteredException() : base("An account with this email already exists.")
    {
    }
}

/// <summary>
/// Deliberately used for both "no such user" and "wrong password" so the API boundary
/// returns an identical 401 in either case — never reveals whether an email is registered.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }
}

/// <summary>
/// The source account's balance (checked atomically, under lock, at posting time - never
/// from an earlier separate read) is less than the amount being debited. A legitimate
/// business outcome, not a bug - mapped to 422 Unprocessable Entity, distinct from the 400s
/// used for malformed input, so a client can tell "fix your request" from "try again later".
/// </summary>
public sealed class InsufficientFundsException : Exception
{
    public InsufficientFundsException() : base("The source wallet does not have sufficient funds for this transfer.")
    {
    }
}

/// <summary>
/// The same (RequestedByUserId, IdempotencyKey) was already used for a request with
/// different parameters (different wallet/amount/currency) - reusing a key for a genuinely
/// different operation is rejected rather than either silently applying the new parameters
/// (a key-reuse vulnerability) or silently returning the old result (which would hide a real
/// client bug). Mapped to 409 Conflict. A replay with matching parameters is NOT this
/// exception - it returns the original result instead.
/// </summary>
public sealed class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException() : base("This idempotency key was already used with different request parameters.")
    {
    }
}

/// <summary>
/// Infrastructure-level signal only, never seen by an API caller: the DB's unique constraint
/// on (RequestedByUserId, IdempotencyKey) was violated on insert, meaning a concurrent
/// request for the same key won the race between this handler's own pre-check
/// (FindByIdempotencyKeyAsync) and its insert attempt. The handler catches this and re-reads
/// by key to decide between returning the winner's result (matching parameters) or throwing
/// IdempotencyKeyConflictException (different parameters) - the DB constraint is the real
/// guarantee, the pre-check is just an optimization to skip the round trip in the common case.
/// </summary>
public sealed class IdempotencyKeyAlreadyUsedException : Exception
{
    public IdempotencyKeyAlreadyUsedException() : base("A transaction with this (RequestedByUserId, IdempotencyKey) already exists.")
    {
    }
}

/// <summary>
/// A transaction can only be reversed once (enforced by a unique filtered index on
/// Transactions.ReversalOfTransactionId - see TransactionConfiguration). Thrown both when
/// ReverseTransactionHandler's own pre-check (FindReversalOfAsync) finds an existing reversal
/// under a different idempotency key, and when that DB constraint is violated on insert by a
/// concurrent request that won the same race - same two-layer pattern as
/// IdempotencyKeyAlreadyUsedException. Mapped to 409 Conflict.
/// </summary>
public sealed class TransactionAlreadyReversedException : Exception
{
    public TransactionAlreadyReversedException() : base("This transaction has already been reversed.")
    {
    }
}
