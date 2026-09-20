namespace MobilityFinance.Messaging;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; init; } = string.Empty;

    public string EventsTopic { get; init; } = "mobility-events";
}
