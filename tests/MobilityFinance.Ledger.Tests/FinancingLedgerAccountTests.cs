using MobilityFinance.Ledger.Domain;

namespace MobilityFinance.Ledger.Tests;

public sealed class FinancingLedgerAccountTests
{
    private static readonly DateTimeOffset OpenedAt =
        new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OpeningAccountCreatesBalancedReceivableTransaction()
    {
        FinancingLedgerAccount account = OpenAccount();

        LedgerTransaction opening = Assert.Single(account.Transactions);

        Assert.Equal(LedgerTransactionType.AccountOpened, opening.Type);
        Assert.Equal(293_600m, Total(opening, EntryDirection.Debit));
        Assert.Equal(293_600m, Total(opening, EntryDirection.Credit));
        Assert.Equal(30_000m, account.Balance.DepositOutstanding.Amount);
        Assert.Equal(263_600m, account.Balance.RepaymentOutstanding.Amount);
    }

    [Fact]
    public void PaymentAllocatesDepositBeforeRepayment()
    {
        FinancingLedgerAccount account = OpenAccount();

        PaymentReceipt receipt = account.RecordPayment(
            "synthetic-provider",
            "payment-001",
            Kes(45_000m),
            OpenedAt.AddDays(1));

        Assert.False(receipt.IsDuplicate);
        Assert.Equal(30_000m, receipt.Payment.DepositApplied.Amount);
        Assert.Equal(15_000m, receipt.Payment.RepaymentApplied.Amount);
        Assert.Equal(0m, receipt.Payment.CustomerCredit.Amount);
        Assert.True(account.Balance.DepositSatisfied);
        Assert.Equal(248_600m, account.Balance.RepaymentOutstanding.Amount);
        Assert.All(account.Transactions, AssertBalanced);
    }

    [Fact]
    public void ExactProviderRetryReturnsOriginalPaymentWithoutPostingAgain()
    {
        FinancingLedgerAccount account = OpenAccount();
        PaymentReceipt original = account.RecordPayment(
            "synthetic-provider",
            "payment-001",
            Kes(45_000m),
            OpenedAt.AddDays(1));

        PaymentReceipt duplicate = account.RecordPayment(
            "SYNTHETIC-PROVIDER",
            "PAYMENT-001",
            Kes(45_000m),
            OpenedAt.AddDays(2));

        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(original.Payment.Id, duplicate.Payment.Id);
        Assert.Equal(2, account.Transactions.Count);
    }

    [Fact]
    public void ConflictingProviderRetryIsRejected()
    {
        FinancingLedgerAccount account = OpenAccount();
        account.RecordPayment(
            "synthetic-provider",
            "payment-001",
            Kes(45_000m),
            OpenedAt.AddDays(1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => account.RecordPayment(
                "synthetic-provider",
                "payment-001",
                Kes(44_000m),
                OpenedAt.AddDays(2)));

        Assert.Contains("different payment data", exception.Message);
        Assert.Equal(2, account.Transactions.Count);
    }

    [Fact]
    public void OverpaymentCreatesCustomerCredit()
    {
        FinancingLedgerAccount account = OpenAccount();

        PaymentReceipt receipt = account.RecordPayment(
            "synthetic-provider",
            "payment-001",
            Kes(300_000m),
            OpenedAt.AddDays(1));

        Assert.True(account.Balance.Settled);
        Assert.Equal(6_400m, receipt.Payment.CustomerCredit.Amount);
        Assert.Equal(6_400m, account.Balance.CustomerCredit.Amount);
        AssertBalanced(account.Transactions[^1]);
    }

    [Fact]
    public void ReversalUsesCompensatingEntriesAndRestoresBalance()
    {
        FinancingLedgerAccount account = OpenAccount();
        PaymentReceipt receipt = account.RecordPayment(
            "synthetic-provider",
            "payment-001",
            Kes(45_000m),
            OpenedAt.AddDays(1));

        PaymentRecord reversed = account.ReversePayment(
            receipt.Payment.Id,
            "provider reversal",
            OpenedAt.AddDays(2));

        Assert.Equal(PaymentStatus.Reversed, reversed.Status);
        Assert.NotNull(reversed.ReversalTransactionId);
        Assert.Equal(30_000m, account.Balance.DepositOutstanding.Amount);
        Assert.Equal(263_600m, account.Balance.RepaymentOutstanding.Amount);
        Assert.Equal(3, account.Transactions.Count);
        AssertBalanced(account.Transactions[^1]);
    }

    private static FinancingLedgerAccount OpenAccount()
    {
        return FinancingLedgerAccount.Open(
            Guid.NewGuid(),
            "SYNTH-3001",
            Kes(30_000m),
            Kes(220_000m),
            Kes(28_600m),
            Kes(15_000m),
            OpenedAt);
    }

    private static LedgerMoney Kes(decimal amount) => new(amount, "KES");

    private static decimal Total(
        LedgerTransaction transaction,
        EntryDirection direction)
    {
        return transaction.Entries
            .Where(entry => entry.Direction == direction)
            .Sum(entry => entry.Amount.Amount);
    }

    private static void AssertBalanced(LedgerTransaction transaction)
    {
        Assert.Equal(
            Total(transaction, EntryDirection.Debit),
            Total(transaction, EntryDirection.Credit));
    }
}
