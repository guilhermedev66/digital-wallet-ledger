using System.Net.Mail;

namespace WalletLedger.Domain.ValueObjects;

public sealed class Email : IEquatable<Email>
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Email cannot be empty.", nameof(input));
        }

        var trimmed = input.Trim();

        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.Ordinal))
            {
                throw new ArgumentException($"'{input}' is not a valid email address.", nameof(input));
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException($"'{input}' is not a valid email address.", nameof(input));
        }

        return new Email(trimmed.ToLowerInvariant());
    }

    public bool Equals(Email? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;
}
