using System.Diagnostics.Metrics;

namespace MobilityFinance.Messaging;

public static class MessagingDiagnostics
{
    public const string MeterName = "MobilityFinance.Messaging";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> PublishedEvents =
        Meter.CreateCounter<long>(
            "mobility.messaging.events.published",
            description: "Integration events published to the broker.");

    public static readonly Counter<long> FailedDeliveries =
        Meter.CreateCounter<long>(
            "mobility.messaging.delivery.failures",
            description: "Integration-event delivery attempts that failed.");

    public static readonly Histogram<double> PublishDuration =
        Meter.CreateHistogram<double>(
            "mobility.messaging.publish.duration",
            unit: "s",
            description: "Time spent publishing an integration event.");
}
