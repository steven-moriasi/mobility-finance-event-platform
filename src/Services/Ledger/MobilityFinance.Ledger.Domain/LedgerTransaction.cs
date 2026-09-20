namespace MobilityFinance.Ledger.Domain;

public sealed record LedgerTransaction
{
    private LedgerTransaction(
        Guid id,
        LedgerTransactionType type,
        string reference,
        DateTimeOffset postedAtUtc,
        IReadOnlyList<LedgerEntry> entries)
    {
        Id = id;
        Type = type;
        Reference = reference;
        PostedAtUtc = postedAtUtc;
        Entries = entries;
    }

    public Guid Id { get; }

    public LedgerTransactionType Type { get; }

    public string Reference { get; }

    public DateTimeOffset PostedAtUtc { get; }

    public IReadOnlyList<LedgerEntry> Entries { get; }

    public static LedgerTransaction Create(
        LedgerTransactionType type,
        string reference,
        DateTimeOffset postedAtUtc,
        params LedgerEntry[] entries)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "A transaction reference is required.",
                nameof(reference));
        }

        if (entries.Length < 2)
        {
            throw new ArgumentException(
                "A transaction requires at least two entries.",
                nameof(entries));
        }

        string currency = entries[0].Amount.Currency;
        if (entries.Any(
                entry => !string.Equals(
                    entry.Amount.Currency,
                    currency,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "All transaction entries must use the same currency.");
        }

        decimal debits = entries
            .Where(entry => entry.Direction == EntryDirection.Debit)
            .Sum(entry => entry.Amount.Amount);
        decimal credits = entries
            .Where(entry => entry.Direction == EntryDirection.Credit)
            .Sum(entry => entry.Amount.Amount);
        if (debits != credits)
        {
            throw new InvalidOperationException(
                "Ledger transaction debits and credits must balance.");
        }

        return new LedgerTransaction(
            Guid.NewGuid(),
            type,
            reference.Trim(),
            postedAtUtc,
            entries);
    }
}

public sealed record LedgerEntry(
    LedgerAccountCode Account,
    EntryDirection Direction,
    LedgerMoney Amount);

public enum LedgerAccountCode
{
    CustomerReceivable,
    AssetFinancing,
    FinanceIncome,
    CashClearing,
    CustomerCredit,
}

public enum EntryDirection
{
    Debit,
    Credit,
}

public enum LedgerTransactionType
{
    AccountOpened,
    PaymentReceived,
    PaymentReversed,
}
