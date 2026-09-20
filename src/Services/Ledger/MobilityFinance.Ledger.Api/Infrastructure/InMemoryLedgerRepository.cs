using MobilityFinance.Ledger.Domain;

namespace MobilityFinance.Ledger.Api.Infrastructure;

public sealed class InMemoryLedgerRepository
{
    private readonly Dictionary<Guid, FinancingLedgerAccount> _accounts = [];
    private readonly Dictionary<Guid, Guid> _accountIdsByAgreement = [];
    private readonly Lock _lock = new();

    public FinancingLedgerAccount AddOrGet(FinancingLedgerAccount account)
    {
        lock (_lock)
        {
            if (_accountIdsByAgreement.TryGetValue(
                    account.AgreementId,
                    out Guid accountId))
            {
                FinancingLedgerAccount existing = _accounts[accountId];
                EnsureMatchingTerms(existing, account);
                return existing;
            }

            _accounts.Add(account.Id, account);
            _accountIdsByAgreement.Add(account.AgreementId, account.Id);
            return account;
        }
    }

    public FinancingLedgerAccount? Get(Guid id)
    {
        lock (_lock)
        {
            return _accounts.GetValueOrDefault(id);
        }
    }

    public IReadOnlyList<FinancingLedgerAccount> List()
    {
        lock (_lock)
        {
            return _accounts.Values
                .OrderByDescending(account => account.OpenedAtUtc)
                .ToArray();
        }
    }

    public T Update<T>(
        Guid id,
        Func<FinancingLedgerAccount, T> update)
    {
        lock (_lock)
        {
            if (!_accounts.TryGetValue(
                    id,
                    out FinancingLedgerAccount? account))
            {
                throw new KeyNotFoundException("Ledger account was not found.");
            }

            return update(account);
        }
    }

    private static void EnsureMatchingTerms(
        FinancingLedgerAccount existing,
        FinancingLedgerAccount candidate)
    {
        if (existing.CustomerReference != candidate.CustomerReference
            || existing.DepositRequired != candidate.DepositRequired
            || existing.Principal != candidate.Principal
            || existing.FinanceCharge != candidate.FinanceCharge
            || existing.ServiceFee != candidate.ServiceFee)
        {
            throw new InvalidOperationException(
                "Agreement already has a ledger account with different terms.");
        }
    }
}
