using System.Net;
using System.Net.Http.Json;
using MobilityFinance.Web.Origination;

namespace MobilityFinance.Web.Tests;

public sealed class OriginationClientTests
{
    [Fact]
    public async Task ListApplicationsMapsOriginationResponse()
    {
        FinancingApplicationDto expected = CreateApplication();
        using HttpClient httpClient = CreateHttpClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { expected }),
            });
        OriginationClient client = new(httpClient);

        IReadOnlyList<FinancingApplicationDto> applications =
            await client.ListApplicationsAsync();

        FinancingApplicationDto application = Assert.Single(applications);
        Assert.Equal(expected.Id, application.Id);
        Assert.Equal("KE-2026.1", application.Offer?.PolicyVersion);
    }

    [Fact]
    public async Task SubmitApplicationPostsToApplicationsEndpoint()
    {
        FinancingApplicationDto expected = CreateApplication();
        using HttpClient httpClient = CreateHttpClient(
            async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    "http://origination.test/applications",
                    request.RequestUri?.ToString());
                CreateApplicationCommand? command =
                    await request.Content!.ReadFromJsonAsync<CreateApplicationCommand>();
                Assert.Equal("SYNTH-3001", command?.ApplicantReference);

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(expected),
                };
            });
        OriginationClient client = new(httpClient);

        OriginationResult<FinancingApplicationDto> result =
            await client.SubmitApplicationAsync(
                new CreateApplicationCommand(
                    "SYNTH-3001",
                    "KE",
                    "KES",
                    250_000m,
                    30_000m,
                    52,
                    12_000m));

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.Id, result.Value?.Id);
    }

    [Fact]
    public async Task FailedRequestReturnsProblemDetail()
    {
        using HttpClient httpClient = CreateHttpClient(
            _ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = JsonContent.Create(
                    new
                    {
                        title = "Pricing declined",
                        detail = "Deposit must be at least 10%.",
                        status = 422,
                    }),
            });
        OriginationClient client = new(httpClient);

        OriginationResult<FinancingApplicationDto> result =
            await client.SubmitApplicationAsync(
                new CreateApplicationCommand(
                    "SYNTH-3001",
                    "KE",
                    "KES",
                    250_000m,
                    10_000m,
                    52,
                    12_000m));

        Assert.False(result.IsSuccess);
        Assert.Equal("Deposit must be at least 10%.", result.Error);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://origination.test/"),
        };
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://origination.test/"),
        };
    }

    private static FinancingApplicationDto CreateApplication()
    {
        DateTimeOffset now = new(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);
        MoneyDto assetPrice = new(250_000m, "KES");
        MoneyDto deposit = new(30_000m, "KES");
        MoneyDto totalRepayable = new(258_600m, "KES");
        FinancingOfferDto offer = new(
            Guid.NewGuid(),
            "KE-2026.1",
            new MoneyDto(220_000m, "KES"),
            new MoneyDto(28_600m, "KES"),
            new MoneyDto(10_000m, "KES"),
            totalRepayable,
            52,
            now,
            now.AddDays(7),
            []);

        return new FinancingApplicationDto(
            Guid.NewGuid(),
            "SYNTH-3001",
            "KE",
            "Priced",
            assetPrice,
            deposit,
            52,
            new MoneyDto(12_000m, "KES"),
            now,
            offer,
            null);
    }

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
