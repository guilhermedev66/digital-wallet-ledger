using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Tests;

public class EmailTests
{
    [Theory]
    [InlineData("foo@example.com", "foo@example.com")]
    [InlineData("Foo@Example.COM", "foo@example.com")]
    [InlineData("  foo@example.com  ", "foo@example.com")]
    [InlineData("first.last+tag@sub.example.co", "first.last+tag@sub.example.co")]
    public void Parse_NormalizesValidAddressesToLowercase(string input, string expected)
    {
        var email = Email.Parse(input);

        Assert.Equal(expected, email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    [InlineData("no-at-sign.example.com")]
    public void Parse_ThrowsArgumentException_ForInvalidInput(string? input)
    {
        Assert.Throws<ArgumentException>(() => Email.Parse(input!));
    }

    [Fact]
    public void Emails_WithSameAddressDifferentCasing_AreEqual()
    {
        var a = Email.Parse("Foo@Example.com");
        var b = Email.Parse("foo@example.COM");

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Emails_WithDifferentAddresses_AreNotEqual()
    {
        var a = Email.Parse("foo@example.com");
        var b = Email.Parse("bar@example.com");

        Assert.NotEqual(a, b);
    }
}
