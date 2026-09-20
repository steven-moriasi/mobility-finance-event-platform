using MobilityFinance.Ledger.Api.Accounts;
using MobilityFinance.Ledger.Api.Infrastructure;
using MobilityFinance.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<InMemoryLedgerRepository>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapLedgerEndpoints();

app.Run();

public partial class Program;
