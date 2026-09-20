using MobilityFinance.Origination.Domain;

namespace MobilityFinance.Origination.Api.Infrastructure;

public sealed class InMemoryOriginationRepository
{
    private readonly Dictionary<Guid, FinancingApplication> _applications = [];
    private readonly Lock _lock = new();

    public void Add(FinancingApplication application)
    {
        lock (_lock)
        {
            _applications.Add(application.Id, application);
        }
    }

    public FinancingApplication? Get(Guid id)
    {
        lock (_lock)
        {
            return _applications.GetValueOrDefault(id);
        }
    }

    public IReadOnlyList<FinancingApplication> List()
    {
        lock (_lock)
        {
            return _applications.Values
                .OrderByDescending(application => application.SubmittedAtUtc)
                .ToArray();
        }
    }

    public T Update<T>(Guid id, Func<FinancingApplication, T> update)
    {
        lock (_lock)
        {
            if (!_applications.TryGetValue(id, out FinancingApplication? application))
            {
                throw new KeyNotFoundException(
                    $"Application {id} was not found.");
            }

            return update(application);
        }
    }
}
