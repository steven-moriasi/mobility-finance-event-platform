namespace MobilityFinance.Web.Security;

public sealed class LocalAccountOptions
{
    public const string SectionName = "LocalAccounts";

    public List<LocalAccountDefinition> Accounts { get; init; } = [];
}

public sealed class LocalAccountDefinition
{
    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required string Password { get; init; }

    public List<string> Roles { get; init; } = [];
}
