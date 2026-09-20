namespace MobilityFinance.Web.Security;

public static class PlatformRoles
{
    public const string Operator = "operator";
    public const string Reviewer = "reviewer";
    public const string PlatformAdmin = "platform-admin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Operator, Reviewer, PlatformAdmin],
        StringComparer.Ordinal);
}
