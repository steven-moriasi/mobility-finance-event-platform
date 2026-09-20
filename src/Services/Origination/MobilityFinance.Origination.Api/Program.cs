using MobilityFinance.Messaging;
using MobilityFinance.Origination.Api.Applications;
using MobilityFinance.Origination.Api.Infrastructure;
using MobilityFinance.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddMobilityEventPublishing(builder.Configuration);
builder.Services.AddSingleton<InMemoryOriginationRepository>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOriginationEndpoints();

app.Run();

public partial class Program;
