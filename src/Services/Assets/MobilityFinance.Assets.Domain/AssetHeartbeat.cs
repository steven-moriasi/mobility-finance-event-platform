namespace MobilityFinance.Assets.Domain;

public sealed record AssetHeartbeat(
    DateTimeOffset RecordedAtUtc,
    decimal BatteryPercent,
    decimal OdometerKm,
    decimal Latitude,
    decimal Longitude);
