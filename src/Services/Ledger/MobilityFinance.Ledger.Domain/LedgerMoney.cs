namespace MobilityFinance.Ledger.Domain;

public readonly record struct LedgerMoney
{
    public LedgerMoney(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static LedgerMoney operator +(LedgerMoney left, LedgerMoney right)
    {
        EnsureSameCurrency(left, right);
        return new LedgerMoney(left.Amount + right.Amount, left.Currency);
    }

    public static LedgerMoney operator -(LedgerMoney left, LedgerMoney right)
    {
        EnsureSameCurrency(left, right);
        return new LedgerMoney(left.Amount - right.Amount, left.Currency);
    }

    private static void EnsureSameCurrency(
        LedgerMoney left,
        LedgerMoney right)
    {
        if (!string.Equals(
                left.Currency,
                right.Currency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Money values must use the same currency.");
        }
    }
}
