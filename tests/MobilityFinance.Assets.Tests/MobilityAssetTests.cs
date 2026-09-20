using MobilityFinance.Assets.Domain;

namespace MobilityFinance.Assets.Tests;

public sealed class MobilityAssetTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AssetCanOnlyBeAssignedOnce()
    {
        MobilityAsset asset = CreateAsset();
        Guid agreementId = Guid.NewGuid();

        bool assigned = asset.Assign(agreementId);
        bool duplicate = asset.Assign(agreementId);

        Assert.True(assigned);
        Assert.False(duplicate);
        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.Equal(agreementId, asset.AgreementId);
        Assert.Throws<InvalidOperationException>(
            () => asset.Assign(Guid.NewGuid()));
    }

    [Fact]
    public void HeartbeatsRequireMonotonicTelemetry()
    {
        MobilityAsset asset = CreateAsset();
        asset.RecordHeartbeat(
            new AssetHeartbeat(
                Now.AddMinutes(1),
                90,
                1200,
                -1.28m,
                36.81m));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => asset.RecordHeartbeat(
                new AssetHeartbeat(
                    Now.AddMinutes(2),
                    89,
                    1199,
                    -1.28m,
                    36.81m)));
    }

    [Fact]
    public void SimulationRecordsTelemetryAndCompletesPendingCommands()
    {
        MobilityAsset asset = CreateAsset();
        asset.Assign(Guid.NewGuid());
        DeviceCommand command = asset.QueueCommand(
            DeviceCommandType.Locate,
            "Confirm synthetic fleet position.",
            Now.AddMinutes(1));

        AssetHeartbeat heartbeat = asset.SimulateDevice(Now.AddMinutes(2));

        Assert.Equal(95, heartbeat.BatteryPercent);
        Assert.Equal(1202.4m, heartbeat.OdometerKm);
        Assert.Equal(DeviceCommandStatus.Completed, command.Status);
        Assert.NotNull(command.CompletedAtUtc);
    }

    private static MobilityAsset CreateAsset()
    {
        return MobilityAsset.Register(
            "MOTO-KE-1001",
            "VIN-SYNTH-1001",
            "Synthetic E-Moto",
            "ke",
            Now);
    }
}
