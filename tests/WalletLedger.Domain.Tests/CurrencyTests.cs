using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Tests;

public sealed class CurrencyTests
{
    [Fact]
    public void SupportedCurrencies_KeepStableValues_AndExpectedMembers()
    {
        Assert.Equal(0, (int)Currency.Usd);
        Assert.Equal(1, (int)Currency.Brl);
        Assert.Equal(2, (int)Currency.Eur);
        Assert.Equal(3, (int)Currency.Gbp);
        Assert.Equal(4, (int)Currency.Chf);
        Assert.Equal(5, (int)Currency.Cad);
        Assert.Equal(6, (int)Currency.Aud);

        Assert.Equal(
            [Currency.Usd, Currency.Brl, Currency.Eur, Currency.Gbp, Currency.Chf, Currency.Cad, Currency.Aud],
            Enum.GetValues<Currency>());
    }
}
