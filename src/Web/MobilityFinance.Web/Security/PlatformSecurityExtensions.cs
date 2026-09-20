using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace MobilityFinance.Web.Security;

public static class PlatformSecurityExtensions
{
    public const string AuthenticationRateLimitPolicy = "authentication";

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
        services.AddRateLimiter(
            options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy(
                    AuthenticationRateLimitPolicy,
                    httpContext => RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown",
                        static _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 5,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1),
                        }));
            });

        return services;
    }

    public static WebApplication UsePlatformSecurityHeaders(
        this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.Use(
            async (context, next) =>
            {
                IHeaderDictionary headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] =
                    "camera=(), geolocation=(), microphone=()";
                headers.ContentSecurityPolicy =
                    "default-src 'self'; "
                    + "base-uri 'self'; "
                    + "connect-src 'self' ws: wss:; "
                    + "font-src 'self'; "
                    + "form-action 'self'; "
                    + "frame-ancestors 'none'; "
                    + "img-src 'self' data:; "
                    + "object-src 'none'; "
                    + "script-src 'self' 'unsafe-inline'; "
                    + "style-src 'self' 'unsafe-inline'";

                await next(context);
            });

        return app;
    }
}
