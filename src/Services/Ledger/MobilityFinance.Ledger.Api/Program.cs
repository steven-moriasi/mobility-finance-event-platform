using MobilityFinance.Ledger.Api.Accounts;
using MobilityFinance.Ledger.Api.Infrastructure;
using MobilityFinance.Messaging;
using MobilityFinance.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddMobilityEventPublishing(builder.Configuration);
builder.Services.AddSingleton<InMemoryLedgerRepository>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapLedgerEndpoints();

app.Run();

public partial class Program;
