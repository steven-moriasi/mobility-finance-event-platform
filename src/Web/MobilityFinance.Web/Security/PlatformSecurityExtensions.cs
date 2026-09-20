using Microsoft.AspNetCore.Authentication.Cookies;

namespace MobilityFinance.Web.Security;

public static class PlatformSecurityExtensions
{
    public static IServiceCollection AddPlatformSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useSecureCookies)
    {
        services
            .AddOptions<LocalAccountOptions>()
            .Bind(configuration.GetSection(LocalAccountOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<LocalAccountStore>();
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(
                options =>
                {
                    options.Cookie.Name = useSecureCookies
                        ? "__Host-MobilityFinance"
                        : "MobilityFinance";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = useSecureCookies
                        ? CookieSecurePolicy.Always
                        : CookieSecurePolicy.SameAsRequest;
                    options.LoginPath = "/login";
                    options.AccessDeniedPath = "/access-denied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = false;
                });

        services.AddAuthorizationBuilder()
            .AddPolicy(
                AuthorizationPolicies.OperatorWork,
                policy => policy.RequireRole(
                    PlatformRoles.Operator,
                    PlatformRoles.PlatformAdmin))
            .AddPolicy(
                AuthorizationPolicies.ReviewWork,
                policy => policy.RequireRole(
                    PlatformRoles.Reviewer,
                    PlatformRoles.PlatformAdmin))
            .AddPolicy(
                AuthorizationPolicies.PlatformAdministration,
                policy => policy.RequireRole(PlatformRoles.PlatformAdmin));

        services.AddCascadingAuthenticationState();

        return services;
    }
}
