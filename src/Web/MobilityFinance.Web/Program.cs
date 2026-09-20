using MobilityFinance.ServiceDefaults;
using MobilityFinance.Web.Assets;
using MobilityFinance.Web.Components;
using MobilityFinance.Web.Ledger;
using MobilityFinance.Web.Origination;
using MobilityFinance.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddPlatformSecurity(
    builder.Configuration,
    useSecureCookies: !builder.Environment.IsDevelopment());
builder.Services.AddHttpClient<OriginationClient>(
    client =>
    {
        string endpoint = builder.Configuration["ServiceEndpoints:Origination"]
            ?? throw new InvalidOperationException(
                "The origination service endpoint is required.");
        client.BaseAddress = new Uri(endpoint, UriKind.Absolute);
    });
builder.Services.AddHttpClient<LedgerClient>(
    client =>
    {
        string endpoint = builder.Configuration["ServiceEndpoints:Ledger"]
            ?? throw new InvalidOperationException(
                "The ledger service endpoint is required.");
        client.BaseAddress = new Uri(endpoint, UriKind.Absolute);
    });
builder.Services.AddHttpClient<AssetClient>(
    client =>
    {
        string endpoint = builder.Configuration["ServiceEndpoints:Assets"]
            ?? throw new InvalidOperationException(
                "The asset service endpoint is required.");
        client.BaseAddress = new Uri(endpoint, UriKind.Absolute);
    });
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.UseServiceDefaults();
app.UsePlatformSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapPlatformAuthentication();
app.MapOriginationBff();
app.MapLedgerBff();
app.MapAssetBff();
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
