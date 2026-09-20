using MobilityFinance.Assets.Api.Assets;
using MobilityFinance.Assets.Api.Infrastructure;
using MobilityFinance.Messaging;
using MobilityFinance.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<InMemoryAssetRepository>();
builder.Services.AddMobilityEventPublishing(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
app.MapAssetEndpoints();
app.MapDefaultEndpoints();

app.Run();

public partial class Program;
