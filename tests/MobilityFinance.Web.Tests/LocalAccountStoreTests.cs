using MobilityFinance.Web.Security;

namespace MobilityFinance.Web.Tests;

public sealed class LocalAccountStoreTests
{
    private static readonly LocalAccountOptions Options = new()
    {
        Accounts =
        [
            new()
            {
                Email = "operator@example.test",
                DisplayName = "Test Operator",
                Password = "correct-password",
                Roles = [PlatformRoles.Operator],
            },
        ],
    };

    [Fact]
    public void ValidCredentialsReturnAccountAndRoles()
    {
        LocalAccountStore store = new(Microsoft.Extensions.Options.Options.Create(Options));

        LocalAccount? account = store.ValidateCredentials(
            "OPERATOR@example.test",
            "correct-password");

        Assert.NotNull(account);
        Assert.Equal("Test Operator", account.DisplayName);
        Assert.Contains(PlatformRoles.Operator, account.Roles);
    }

    [Theory]
    [InlineData("unknown@example.test", "correct-password")]
    [InlineData("operator@example.test", "wrong-password")]
    public void InvalidCredentialsAreRejected(string email, string password)
    {
        LocalAccountStore store = new(Microsoft.Extensions.Options.Options.Create(Options));

        LocalAccount? account = store.ValidateCredentials(email, password);

        Assert.Null(account);
    }

    [Fact]
    public void UnknownRolesFailAtStartup()
    {
        LocalAccountOptions options = new()
        {
            Accounts =
            [
                new()
                {
                    Email = "unknown@example.test",
                    DisplayName = "Unknown Role",
                    Password = "password",
                    Roles = ["super-user"],
                },
            ],
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new LocalAccountStore(
                Microsoft.Extensions.Options.Options.Create(options)));

        Assert.Contains("invalid configuration", exception.Message, StringComparison.Ordinal);
    }
}
