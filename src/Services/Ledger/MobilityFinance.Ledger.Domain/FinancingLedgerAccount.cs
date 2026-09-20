namespace MobilityFinance.Ledger.Domain;

public sealed class FinancingLedgerAccount
{
    private readonly List<LedgerTransaction> _transactions = [];
    private readonly List<PaymentRecord> _payments = [];

    private FinancingLedgerAccount(
        Guid id,
        Guid agreementId,
        string customerReference,
        LedgerMoney depositRequired,
        LedgerMoney principal,
        LedgerMoney financeCharge,
        LedgerMoney serviceFee,
        DateTimeOffset openedAtUtc)
    {
        Id = id;
        AgreementId = agreementId;
        CustomerReference = customerReference;
        DepositRequired = depositRequired;
        Principal = principal;
        FinanceCharge = financeCharge;
        ServiceFee = serviceFee;
        OpenedAtUtc = openedAtUtc;
    }

    public Guid Id { get; }

    public Guid AgreementId { get; }

    public string CustomerReference { get; }

    public LedgerMoney DepositRequired { get; }

    public LedgerMoney Principal { get; }

    public LedgerMoney FinanceCharge { get; }

    public LedgerMoney ServiceFee { get; }

    public DateTimeOffset OpenedAtUtc { get; }

    public IReadOnlyList<LedgerTransaction> Transactions => _transactions;

    public IReadOnlyList<PaymentRecord> Payments => _payments;

    public LedgerBalance Balance
    {
        get
        {
            decimal depositPaid = PostedPayments.Sum(
                payment => payment.DepositApplied.Amount);
            decimal repaymentPaid = PostedPayments.Sum(
                payment => payment.RepaymentApplied.Amount);
            decimal customerCredit = PostedPayments.Sum(
                payment => payment.CustomerCredit.Amount);
            LedgerMoney totalRepayable = Principal + FinanceCharge + ServiceFee;

            return new LedgerBalance(
                new LedgerMoney(
                    Math.Max(0, DepositRequired.Amount - depositPaid),
                    DepositRequired.Currency),
                new LedgerMoney(
                    Math.Max(0, totalRepayable.Amount - repaymentPaid),
                    DepositRequired.Currency),
                new LedgerMoney(customerCredit, DepositRequired.Currency));
        }
    }

    private IEnumerable<PaymentRecord> PostedPayments =>
        _payments.Where(payment => payment.Status == PaymentStatus.Posted);

    public static FinancingLedgerAccount Open(
        Guid agreementId,
        string customerReference,
        LedgerMoney depositRequired,
        LedgerMoney principal,
        LedgerMoney financeCharge,
        LedgerMoney serviceFee,
        DateTimeOffset openedAtUtc)
    {
        if (agreementId == Guid.Empty)
        {
            throw new ArgumentException(
                "Agreement ID is required.",
                nameof(agreementId));
        }

        if (string.IsNullOrWhiteSpace(customerReference))
        {
            throw new ArgumentException(
                "Customer reference is required.",
                nameof(customerReference));
        }

        EnsureTermsAreValid(
            depositRequired,
            principal,
            financeCharge,
            serviceFee);

        FinancingLedgerAccount account = new(
            Guid.NewGuid(),
            agreementId,
            customerReference.Trim(),
            depositRequired,
            principal,
            financeCharge,
            serviceFee,
            openedAtUtc);
        account.RecordOpeningTransaction();
        return account;
    }

    public PaymentReceipt RecordPayment(
        string provider,
        string providerTransactionId,
        LedgerMoney amount,
        DateTimeOffset receivedAtUtc)
    {
        string providerKey = NormalizeRequired(provider, nameof(provider));
        string transactionKey = NormalizeRequired(
            providerTransactionId,
            nameof(providerTransactionId));
        if (amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Payment amount must be positive.");
        }

        EnsureCurrency(amount);
        PaymentRecord? existing = _payments.SingleOrDefault(
            payment => string.Equals(
                    payment.Provider,
                    providerKey,
                    StringComparison.Ordinal)
                && string.Equals(
                    payment.ProviderTransactionId,
                    transactionKey,
                    StringComparison.Ordinal));
        if (existing is not null)
        {
            if (existing.Amount != amount)
            {
                throw new InvalidOperationException(
                    "Provider transaction ID was already recorded with different payment data.");
            }

            return new PaymentReceipt(existing, IsDuplicate: true);
        }

        LedgerBalance balance = Balance;
        decimal depositApplied = Math.Min(
            amount.Amount,
            balance.DepositOutstanding.Amount);
        decimal afterDeposit = amount.Amount - depositApplied;
        decimal repaymentApplied = Math.Min(
            afterDeposit,
            balance.RepaymentOutstanding.Amount);
        decimal customerCredit = afterDeposit - repaymentApplied;
        LedgerMoney applied = new(
            depositApplied + repaymentApplied,
            amount.Currency);

        List<LedgerEntry> entries =
        [
            new(
                LedgerAccountCode.CashClearing,
                EntryDirection.Debit,
                amount),
        ];
        AddEntryWhenPositive(
            entries,
            LedgerAccountCode.CustomerReceivable,
            EntryDirection.Credit,
            applied);
        AddEntryWhenPositive(
            entries,
            LedgerAccountCode.CustomerCredit,
            EntryDirection.Credit,
            new LedgerMoney(customerCredit, amount.Currency));

        LedgerTransaction transaction = LedgerTransaction.Create(
            LedgerTransactionType.PaymentReceived,
            $"{providerKey}:{transactionKey}",
            receivedAtUtc,
            [.. entries]);
        PaymentRecord payment = new(
            Guid.NewGuid(),
            providerKey,
            transactionKey,
            amount,
            new LedgerMoney(depositApplied, amount.Currency),
            new LedgerMoney(repaymentApplied, amount.Currency),
            new LedgerMoney(customerCredit, amount.Currency),
            transaction.Id,
            receivedAtUtc,
            PaymentStatus.Posted,
            null);

        _transactions.Add(transaction);
        _payments.Add(payment);
        return new PaymentReceipt(payment, IsDuplicate: false);
    }

    public PaymentRecord ReversePayment(
        Guid paymentId,
        string reason,
        DateTimeOffset reversedAtUtc)
    {
        int index = _payments.FindIndex(payment => payment.Id == paymentId);
        if (index < 0)
        {
            throw new KeyNotFoundException("Payment was not found.");
        }

        PaymentRecord payment = _payments[index];
        if (payment.Status == PaymentStatus.Reversed)
        {
            throw new InvalidOperationException("Payment was already reversed.");
        }

        string reversalReason = NormalizeRequired(reason, nameof(reason));
        List<LedgerEntry> entries = [];
        LedgerMoney applied = payment.DepositApplied + payment.RepaymentApplied;
        AddEntryWhenPositive(
            entries,
            LedgerAccountCode.CustomerReceivable,
            EntryDirection.Debit,
            applied);
        AddEntryWhenPositive(
            entries,
            LedgerAccountCode.CustomerCredit,
            EntryDirection.Debit,
            payment.CustomerCredit);
        entries.Add(
            new LedgerEntry(
                LedgerAccountCode.CashClearing,
                EntryDirection.Credit,
                payment.Amount));

        LedgerTransaction transaction = LedgerTransaction.Create(
            LedgerTransactionType.PaymentReversed,
            $"{payment.Provider}:{payment.ProviderTransactionId}:{reversalReason}",
            reversedAtUtc,
            [.. entries]);
        PaymentRecord reversed = payment with
        {
            Status = PaymentStatus.Reversed,
            ReversalTransactionId = transaction.Id,
        };

        _transactions.Add(transaction);
        _payments[index] = reversed;
        return reversed;
    }

    private static void EnsureTermsAreValid(
        LedgerMoney depositRequired,
        LedgerMoney principal,
        LedgerMoney financeCharge,
        LedgerMoney serviceFee)
    {
        if (depositRequired.Amount < 0
            || principal.Amount <= 0
            || financeCharge.Amount < 0
            || serviceFee.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(principal),
                "Ledger terms must contain a positive principal and non-negative charges.");
        }

        _ = depositRequired + principal + financeCharge + serviceFee;
    }

    private void RecordOpeningTransaction()
    {
        LedgerMoney financedAsset = DepositRequired + Principal;
        LedgerMoney income = FinanceCharge + ServiceFee;
        LedgerMoney expected = financedAsset + income;
        List<LedgerEntry> entries =
        [
            new(
                LedgerAccountCode.CustomerReceivable,
                EntryDirection.Debit,
                expected),
            new(
                LedgerAccountCode.AssetFinancing,
                EntryDirection.Credit,
                financedAsset),
        ];
        AddEntryWhenPositive(
            entries,
            LedgerAccountCode.FinanceIncome,
            EntryDirection.Credit,
            income);

        _transactions.Add(
            LedgerTransaction.Create(
                LedgerTransactionType.AccountOpened,
                AgreementId.ToString(),
                OpenedAtUtc,
                [.. entries]));
    }

    private static void AddEntryWhenPositive(
        List<LedgerEntry> entries,
        LedgerAccountCode account,
        EntryDirection direction,
        LedgerMoney amount)
    {
        if (amount.Amount > 0)
        {
            entries.Add(new LedgerEntry(account, direction, amount));
        }
    }

    private void EnsureCurrency(LedgerMoney amount)
    {
        if (!string.Equals(
                amount.Currency,
                DepositRequired.Currency,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Payment currency must match the ledger account currency.");
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A non-empty value is required.",
                parameterName);
        }

        return value.Trim().ToUpperInvariant();
    }
}

public sealed record PaymentReceipt(PaymentRecord Payment, bool IsDuplicate);

public sealed record PaymentRecord(
    Guid Id,
    string Provider,
    string ProviderTransactionId,
    LedgerMoney Amount,
    LedgerMoney DepositApplied,
    LedgerMoney RepaymentApplied,
    LedgerMoney CustomerCredit,
    Guid LedgerTransactionId,
    DateTimeOffset ReceivedAtUtc,
    PaymentStatus Status,
    Guid? ReversalTransactionId);

public sealed record LedgerBalance(
    LedgerMoney DepositOutstanding,
    LedgerMoney RepaymentOutstanding,
    LedgerMoney CustomerCredit)
{
    public bool DepositSatisfied => DepositOutstanding.Amount == 0;

    public bool Settled => DepositSatisfied && RepaymentOutstanding.Amount == 0;
}

public enum PaymentStatus
{
    Posted,
    Reversed,
}
