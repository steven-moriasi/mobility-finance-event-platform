using MobilityFinance.Activation.Worker;
using MobilityFinance.Activation.Worker.Workflows;
using MobilityFinance.Messaging;
using MobilityFinance.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddMobilityEventPublishing(builder.Configuration);
builder.Services.AddSingleton<ActivationWorkflowStore>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet(
    "/workflows",
    (ActivationWorkflowStore store) =>
        Results.Ok(store.List().Select(ActivationWorkflowResponse.From)));
app.MapGet(
    "/workflows/{agreementId:guid}",
    (Guid agreementId, ActivationWorkflowStore store) =>
    {
        ActivationWorkflow? workflow = store.Get(agreementId);
        return workflow is null
            ? Results.NotFound()
            : Results.Ok(ActivationWorkflowResponse.From(workflow));
    });

app.Run();
