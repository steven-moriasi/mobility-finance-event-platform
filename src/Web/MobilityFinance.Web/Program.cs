using MobilityFinance.ServiceDefaults;
using MobilityFinance.Web.Components;
using MobilityFinance.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddPlatformSecurity(
    builder.Configuration,
    useSecureCookies: !builder.Environment.IsDevelopment());
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapPlatformAuthentication();
app.MapGet(
        "/bff/me",
        (HttpContext context) => Results.Ok(
            new
            {
                name = context.User.Identity?.Name,
                roles = context.User.FindAll(System.Security.Claims.ClaimTypes.Role)
                    .Select(claim => claim.Value),
            }))
    .RequireAuthorization();
app.MapGet(
        "/bff/operator/workspace",
        () => Results.Ok(new { workspace = "operator" }))
    .RequireAuthorization(AuthorizationPolicies.OperatorWork);
app.MapGet(
        "/bff/review/workspace",
        () => Results.Ok(new { workspace = "review" }))
    .RequireAuthorization(AuthorizationPolicies.ReviewWork);
app.MapGet(
        "/bff/platform/workspace",
        () => Results.Ok(new { workspace = "platform-administration" }))
    .RequireAuthorization(AuthorizationPolicies.PlatformAdministration);
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
