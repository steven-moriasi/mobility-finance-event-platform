using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace MobilityFinance.Web.Security;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapPlatformAuthentication(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder authentication = endpoints
            .MapGroup("/auth")
            .ExcludeFromDescription();

        authentication
            .MapPost(
                "/login",
                async (
                    [FromForm] LoginRequest request,
                    LocalAccountStore accounts,
                    HttpContext httpContext) =>
                {
                    LocalAccount? account = accounts.ValidateCredentials(
                        request.Email,
                        request.Password);

                    if (account is null)
                    {
                        return Results.LocalRedirect(
                            $"/login?error=invalid&returnUrl={Uri.EscapeDataString(GetReturnUrl(request.ReturnUrl))}");
                    }

                    Claim[] claims =
                    [
                        new(ClaimTypes.NameIdentifier, account.Email),
                        new(ClaimTypes.Name, account.DisplayName),
                        new(ClaimTypes.Email, account.Email),
                        .. account.Roles.Select(role => new Claim(ClaimTypes.Role, role)),
                    ];
                    ClaimsIdentity identity = new(
                        claims,
                        CookieAuthenticationDefaults.AuthenticationScheme);
                    ClaimsPrincipal principal = new(identity);
                    AuthenticationProperties properties = new()
                    {
                        AllowRefresh = false,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                        IsPersistent = false,
                    };

                    await httpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        properties);

                    return Results.LocalRedirect(GetReturnUrl(request.ReturnUrl));
                })
            .AllowAnonymous();

        authentication
            .MapPost(
                "/logout",
                async (HttpContext httpContext) =>
                {
                    await httpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);

                    return Results.LocalRedirect("/login");
                })
            .RequireAuthorization();

        return endpoints;
    }

    private static string GetReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl[0] != '/'
            || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    public sealed record LoginRequest(
        string Email,
        string Password,
        string? ReturnUrl);
}
