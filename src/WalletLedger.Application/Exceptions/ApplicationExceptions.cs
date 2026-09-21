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
