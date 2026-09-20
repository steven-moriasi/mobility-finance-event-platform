using System.Net;
using System.Net.Http.Json;
using MobilityFinance.Web.Ledger;

namespace MobilityFinance.Web.Tests;

public sealed class LedgerClientTests
{
    [Fact]
    public async Task ListAccountsMapsBalancesAndJournal()
    {
        LedgerAccountDto expected = CreateAccount();
        using HttpClient httpClient = CreateHttpClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { expected }),
            });
        LedgerClient client = new(httpClient);

        IReadOnlyList<LedgerAccountDto> accounts =
            await client.ListAccountsAsync();

        LedgerAccountDto account = Assert.Single(accounts);
        Assert.Equal(expected.Id, account.Id);
        Assert.Equal(45_000m, account.Balance.DepositOutstanding.Amount);
        Assert.Single(account.Transactions);
    }

    [Fact]
    public async Task RecordPaymentUsesAccountPaymentEndpoint()
    {
        Guid accountId = Guid.NewGuid();
        PaymentReceiptDto expected = new(
            new PaymentDto(
                Guid.NewGuid(),
                "SYNTHETIC-PROVIDER",
                "PAY-2001",
                Kes(60_000m),
                Kes(45_000m),
                Kes(15_000m),
                Kes(0m),
                DateTimeOffset.UtcNow,
                "Posted",
                Guid.NewGuid(),
                null),
            IsDuplicate: false);
        using HttpClient httpClient = CreateHttpClient(
            async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    $"http://ledger.test/accounts/{accountId}/payments",
                    request.RequestUri?.ToString());
                RecordPaymentCommand? command =
                    await request.Content!.ReadFromJsonAsync<RecordPaymentCommand>();
                Assert.Equal("PAY-2001", command?.ProviderTransactionId);
                Assert.Equal(60_000m, command?.Amount);

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(expected),
                };
            });
        LedgerClient client = new(httpClient);

        LedgerResult<PaymentReceiptDto> result =
            await client.RecordPaymentAsync(
                accountId,
                new RecordPaymentCommand(
                    "SYNTHETIC-PROVIDER",
                    "PAY-2001",
                    "KES",
                    60_000m));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.Payment.Id, result.Value?.Payment.Id);
    }

    [Fact]
    public async Task ConflictingProviderEventReturnsProblemDetail()
    {
        using HttpClient httpClient = CreateHttpClient(
            _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(
                    new
                    {
                        title = "Ledger request conflict",
                        detail = "Provider transaction ID has different payment data.",
                        status = 409,
                    }),
            });
        LedgerClient client = new(httpClient);

        LedgerResult<PaymentReceiptDto> result =
            await client.RecordPaymentAsync(
                Guid.NewGuid(),
                new RecordPaymentCommand(
                    "SYNTHETIC-PROVIDER",
                    "PAY-2001",
                    "KES",
                    55_000m));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            "Provider transaction ID has different payment data.",
            result.Error);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://ledger.test/"),
        };
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://ledger.test/"),
        };
    }

    private static LedgerAccountDto CreateAccount()
    {
        DateTimeOffset now = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        LedgerTransactionDto opening = new(
            Guid.NewGuid(),
            "AccountOpened",
            Guid.NewGuid().ToString(),
            now,
            []);

        return new LedgerAccountDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SYNTH-3001",
            now,
            Kes(45_000m),
            Kes(255_000m),
            Kes(33_150m),
            Kes(12_000m),
            new LedgerBalanceDto(
                Kes(45_000m),
                Kes(300_150m),
                Kes(0m),
                DepositSatisfied: false,
                Settled: false),
            [],
            [opening]);
    }

    private static LedgerMoneyDto Kes(decimal amount) => new(amount, "KES");

    private sealed class StubHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responder)
            : this(request => Task.FromResult(responder(request)))
        {
        }

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _responder(request);
        }
    }
}
