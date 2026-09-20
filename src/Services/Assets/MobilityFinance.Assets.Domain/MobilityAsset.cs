namespace MobilityFinance.Assets.Domain;

public sealed class MobilityAsset
{
    private readonly List<AssetHeartbeat> _heartbeats = [];
    private readonly List<DeviceCommand> _commands = [];

    private MobilityAsset(
        string reference,
        string serialNumber,
        string model,
        string market,
        DateTimeOffset registeredAtUtc)
    {
        Id = Guid.NewGuid();
        Reference = reference;
        SerialNumber = serialNumber;
        Model = model;
        Market = market;
        RegisteredAtUtc = registeredAtUtc;
        Status = AssetStatus.Available;
        Version = 1;
    }

    public Guid Id { get; }

    public string Reference { get; }

    public string SerialNumber { get; }

    public string Model { get; }

    public string Market { get; }

    public DateTimeOffset RegisteredAtUtc { get; }

    public AssetStatus Status { get; private set; }

    public Guid? AgreementId { get; private set; }

    public long Version { get; private set; }

    public AssetHeartbeat? LastHeartbeat => _heartbeats.LastOrDefault();

    public IReadOnlyList<AssetHeartbeat> Heartbeats => _heartbeats;

    public IReadOnlyList<DeviceCommand> Commands => _commands;

    public static MobilityAsset Register(
        string reference,
        string serialNumber,
        string model,
        string market,
        DateTimeOffset registeredAtUtc)
    {
        return new MobilityAsset(
            Required(reference, nameof(reference)),
            Required(serialNumber, nameof(serialNumber)),
            Required(model, nameof(model)),
            Required(market, nameof(market)).ToUpperInvariant(),
            registeredAtUtc);
    }

    public bool Assign(Guid agreementId)
    {
        if (agreementId == Guid.Empty)
        {
            throw new ArgumentException(
                "Agreement ID is required.",
                nameof(agreementId));
        }

        if (AgreementId == agreementId)
        {
            return false;
        }

        if (Status != AssetStatus.Available)
        {
            throw new InvalidOperationException(
                "Only available assets can be assigned.");
        }

        AgreementId = agreementId;
        Status = AssetStatus.Assigned;
        Version++;
        return true;
    }

    public void RecordHeartbeat(AssetHeartbeat heartbeat)
    {
        if (heartbeat.RecordedAtUtc < RegisteredAtUtc
            || LastHeartbeat is not null
            && heartbeat.RecordedAtUtc <= LastHeartbeat.RecordedAtUtc)
        {
            throw new InvalidOperationException(
                "Heartbeat timestamps must increase.");
        }

        if (heartbeat.BatteryPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heartbeat),
                "Battery percentage must be between zero and 100.");
        }

        if (heartbeat.OdometerKm < 0
            || LastHeartbeat is not null
            && heartbeat.OdometerKm < LastHeartbeat.OdometerKm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heartbeat),
                "Odometer readings cannot decrease.");
        }

        _heartbeats.Add(heartbeat);
        Version++;
    }

    public DeviceCommand QueueCommand(
        DeviceCommandType type,
        string reason,
        DateTimeOffset requestedAtUtc)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Device command type is not supported.");
        }

        if (Status != AssetStatus.Assigned)
        {
            throw new InvalidOperationException(
                "Commands require an assigned asset.");
        }

        DeviceCommand command = new(
            type,
            Required(reason, nameof(reason)),
            requestedAtUtc);
        _commands.Add(command);
        Version++;
        return command;
    }

    public AssetHeartbeat SimulateDevice(DateTimeOffset recordedAtUtc)
    {
        if (Status != AssetStatus.Assigned)
        {
            throw new InvalidOperationException(
                "Device simulation requires an assigned asset.");
        }

        decimal battery = Math.Max(0, (LastHeartbeat?.BatteryPercent ?? 96) - 1);
        decimal odometer = (LastHeartbeat?.OdometerKm ?? 1200) + 2.4m;
        AssetHeartbeat heartbeat = new(
            recordedAtUtc,
            battery,
            odometer,
            -1.286389m,
            36.817223m);
        RecordHeartbeat(heartbeat);

        foreach (DeviceCommand command in _commands.Where(
                     command => command.Status == DeviceCommandStatus.Pending))
        {
            command.Complete(
                recordedAtUtc,
                succeeded: true,
                $"Synthetic device acknowledged {command.Type}.");
        }

        return heartbeat;
    }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A non-empty value is required.",
                parameterName);
        }

        return value.Trim();
    }
}
