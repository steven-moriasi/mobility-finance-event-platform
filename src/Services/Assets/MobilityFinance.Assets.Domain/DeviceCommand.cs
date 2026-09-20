namespace MobilityFinance.Assets.Domain;

public enum DeviceCommandType
{
    Locate,
    Lock,
    Unlock,
}

public enum DeviceCommandStatus
{
    Pending,
    Completed,
    Failed,
}

public sealed class DeviceCommand
{
    internal DeviceCommand(
        DeviceCommandType type,
        string reason,
        DateTimeOffset requestedAtUtc)
    {
        Id = Guid.NewGuid();
        Type = type;
        Reason = reason;
        RequestedAtUtc = requestedAtUtc;
    }

    public Guid Id { get; }

    public DeviceCommandType Type { get; }

    public string Reason { get; }

    public DateTimeOffset RequestedAtUtc { get; }

    public DeviceCommandStatus Status { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? Result { get; private set; }

    internal void Complete(
        DateTimeOffset completedAtUtc,
        bool succeeded,
        string result)
    {
        if (Status != DeviceCommandStatus.Pending)
        {
            return;
        }

        Status = succeeded
            ? DeviceCommandStatus.Completed
            : DeviceCommandStatus.Failed;
        CompletedAtUtc = completedAtUtc;
        Result = result;
    }
}
