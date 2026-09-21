using WalletLedger.Domain.Entities;
using WalletLedger.Domain.ValueObjects;

namespace WalletLedger.Domain.Tests;

public class UserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_ThrowsArgumentException_WhenPasswordHashIsEmpty(string? passwordHash)
    {
        var email = Email.Parse("foo@example.com");

        Assert.Throws<ArgumentException>(() => User.Register(email, passwordHash!));
    }

    [Fact]
    public void Register_SetsExpectedFields_OnSuccess()
    {
        var email = Email.Parse("foo@example.com");
        var before = DateTime.UtcNow;

        var user = User.Register(email, "some-hash");

        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal("some-hash", user.PasswordHash);
        Assert.False(user.IsAdmin);
        Assert.InRange(user.CreatedAtUtc, before, after);
    }

    [Fact]
    public void PromoteToAdmin_SetsIsAdminTrue()
    {
        var user = User.Register(Email.Parse("foo@example.com"), "some-hash");

        user.PromoteToAdmin();

        Assert.True(user.IsAdmin);
    }

    [Fact]
    public void Register_GeneratesDistinctIds_ForDistinctUsers()
    {
        var email = Email.Parse("foo@example.com");

        var first = User.Register(email, "hash-1");
        var second = User.Register(email, "hash-2");

        Assert.NotEqual(first.Id, second.Id);
    }
}
