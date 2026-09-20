using System.Diagnostics.Metrics;

namespace MobilityFinance.Activation.Worker;

public static class ActivationDiagnostics
{
    public const string MeterName = "MobilityFinance.Activation";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ProcessedEvents =
        Meter.CreateCounter<long>(
            "mobility.activation.events.processed",
            description: "Events completed by the activation worker.");

    public static readonly Counter<long> DuplicateEvents =
        Meter.CreateCounter<long>(
            "mobility.activation.events.duplicates",
            description: "Duplicate events ignored by the activation inbox.");

    public static readonly Counter<long> ActivatedAgreements =
        Meter.CreateCounter<long>(
            "mobility.activation.agreements.completed",
            description: "Agreement activation workflows completed.");

    public static readonly Counter<long> DeadLetteredEvents =
        Meter.CreateCounter<long>(
            "mobility.activation.events.dead_lettered",
            description: "Invalid activation events moved to dead letters.");
}
