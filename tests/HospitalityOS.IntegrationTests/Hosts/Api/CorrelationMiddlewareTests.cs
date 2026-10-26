using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

public sealed class CorrelationMiddlewareTests
{
    private const string Header = "X-Correlation-Id";

    [Fact]
    public async Task Request_WithCorrelationId_JoinsAndReturnsTheCallersFlow()
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        Guid callerFlow = Guid.CreateVersion7();
        using HttpRequestMessage request = new(HttpMethod.Get, "/health");
        request.Headers.Add(Header, callerFlow.ToString());

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(callerFlow.ToString(), Assert.Single(response.Headers.GetValues(Header)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Request_WithoutUsableCorrelationId_StartsANewFlow(string? sent)
    {
        await using WebApplicationFactory<Program> factory = new();
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, "/health");

        if (sent is not null)
        {
            request.Headers.TryAddWithoutValidation(Header, sent);
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Guid returned = Guid.Parse(Assert.Single(response.Headers.GetValues(Header)));
        Assert.NotEqual(Guid.Empty, returned);
        Assert.Equal(7, returned.Version);
    }
}
