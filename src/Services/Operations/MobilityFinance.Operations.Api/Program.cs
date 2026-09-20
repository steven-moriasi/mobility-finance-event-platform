using MobilityFinance.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.UseServiceDefaults();
app.MapDefaultEndpoints();

app.Run();
