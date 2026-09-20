using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MobilityFinance.Web.Security;

public sealed class LocalAccountStore
{
    private readonly PasswordHasher<LocalAccount> _passwordHasher = new();
    private readonly Dictionary<string, StoredLocalAccount> _accounts;

    public LocalAccountStore(IOptions<LocalAccountOptions> options)
    {
        Dictionary<string, StoredLocalAccount> accounts =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (LocalAccountDefinition definition in options.Value.Accounts)
        {
            string email = definition.Email.Trim();
            string[] roles = definition.Roles
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (email.Length == 0
                || definition.DisplayName.Trim().Length == 0
                || definition.Password.Length == 0
                || roles.Length == 0
                || roles.Any(role => !PlatformRoles.All.Contains(role)))
            {
                throw new InvalidOperationException(
                    $"Local account '{definition.Email}' has invalid configuration.");
            }

            LocalAccount account = new(email, definition.DisplayName.Trim(), roles);
            string passwordHash = _passwordHasher.HashPassword(account, definition.Password);

            if (!accounts.TryAdd(email, new StoredLocalAccount(account, passwordHash)))
            {
                throw new InvalidOperationException(
                    $"Local account '{definition.Email}' is configured more than once.");
            }
        }

        if (accounts.Count == 0)
        {
            throw new InvalidOperationException("At least one local account is required.");
        }

        _accounts = accounts;
    }

    public LocalAccount? ValidateCredentials(string email, string password)
    {
        if (!_accounts.TryGetValue(email.Trim(), out StoredLocalAccount? storedAccount))
        {
            return null;
        }

        PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(
            storedAccount.Account,
            storedAccount.PasswordHash,
            password);

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded
            ? storedAccount.Account
            : null;
    }

    private sealed record StoredLocalAccount(
        LocalAccount Account,
        string PasswordHash);
}

public sealed record LocalAccount(
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles);
