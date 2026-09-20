using System.Net;
using System.Net.Http.Json;
using MobilityFinance.Web.Assets;

namespace MobilityFinance.Web.Tests;

public sealed class AssetClientTests
{
    [Fact]
    public async Task AssignAssetUsesAgreementEndpoint()
    {
        Guid assetId = Guid.NewGuid();
        Guid agreementId = Guid.NewGuid();
        AssetAssignmentDto expected = new(
            CreateAsset(assetId, agreementId),
            IsDuplicate: false);
        using HttpClient httpClient = CreateHttpClient(
            async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    $"http://assets.test/assets/{assetId}/assign",
                    request.RequestUri?.ToString());
                AssignAssetCommand? command =
                    await request.Content!.ReadFromJsonAsync<AssignAssetCommand>();
                Assert.Equal(agreementId, command?.AgreementId);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(expected),
                };
            });
        AssetClient client = new(httpClient);

        AssetResult<AssetAssignmentDto> result =
            await client.AssignAssetAsync(assetId, agreementId);

        Assert.True(result.IsSuccess);
        Assert.Equal(agreementId, result.Value?.Asset.AgreementId);
    }

    [Fact]
    public async Task SimulateDeviceMapsLatestHeartbeat()
    {
        Guid assetId = Guid.NewGuid();
        AssetDto expected = CreateAsset(assetId, Guid.NewGuid());
        using HttpClient httpClient = CreateHttpClient(
            request =>
            {
                Assert.Equal(
                    $"http://assets.test/assets/{assetId}/simulation/tick",
                    request.RequestUri?.ToString());
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(expected),
                };
            });
        AssetClient client = new(httpClient);

        AssetResult<AssetDto> result =
            await client.SimulateDeviceAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(95, result.Value?.LastHeartbeat?.BatteryPercent);
    }

    [Fact]
    public async Task AssetConflictReturnsProblemDetail()
    {
        using HttpClient httpClient = CreateHttpClient(
            _ => new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(
                    new
                    {
                        title = "Asset request conflict",
                        detail = "Only available assets can be assigned.",
                        status = 409,
                    }),
            });
        AssetClient client = new(httpClient);

        AssetResult<AssetAssignmentDto> result =
            await client.AssignAssetAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("Only available assets can be assigned.", result.Error);
    }

    private static AssetDto CreateAsset(Guid assetId, Guid agreementId)
    {
        DateTimeOffset now = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        return new AssetDto(
            assetId,
            "MOTO-KE-1001",
            "VIN-SYNTH-1001",
            "Synthetic E-Moto",
            "KE",
            now.AddHours(-1),
            "Assigned",
            agreementId,
            3,
            new HeartbeatDto(
                now,
                95,
                1202.4m,
                -1.286389m,
                36.817223m),
            []);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://assets.test/"),
        };
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new StubHttpMessageHandler(responder))
        {
            BaseAddress = new Uri("http://assets.test/"),
        };
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
