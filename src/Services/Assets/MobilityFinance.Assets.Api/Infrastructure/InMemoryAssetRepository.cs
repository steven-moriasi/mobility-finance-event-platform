using MobilityFinance.Assets.Domain;

namespace MobilityFinance.Assets.Api.Infrastructure;

public sealed class InMemoryAssetRepository
{
    private readonly Dictionary<Guid, MobilityAsset> _assets = [];
    private readonly Lock _lock = new();

    public void Add(MobilityAsset asset)
    {
        lock (_lock)
        {
            if (_assets.Values.Any(
                    existing => existing.Reference.Equals(
                            asset.Reference,
                            StringComparison.OrdinalIgnoreCase)
                        || existing.SerialNumber.Equals(
                            asset.SerialNumber,
                            StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Asset reference and serial number must be unique.");
            }

            _assets.Add(asset.Id, asset);
        }
    }

    public MobilityAsset? Get(Guid id)
    {
        lock (_lock)
        {
            return _assets.GetValueOrDefault(id);
        }
    }

    public IReadOnlyList<MobilityAsset> List()
    {
        lock (_lock)
        {
            return _assets.Values
                .OrderByDescending(asset => asset.RegisteredAtUtc)
                .ToArray();
        }
    }

    public T Update<T>(Guid id, Func<MobilityAsset, T> update)
    {
        lock (_lock)
        {
            if (!_assets.TryGetValue(id, out MobilityAsset? asset))
            {
                throw new KeyNotFoundException("Asset was not found.");
            }

            return update(asset);
        }
    }
}
