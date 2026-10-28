using System.Text.Json;
using HospitalityOS.Capabilities.Execution;
using HospitalityOS.Contracts.Capabilities.Properties;
using HospitalityOS.Contracts.Serialization;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Execution;
using HospitalityOS.Ports.Search;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.IntegrationTests.Capabilities.Reading;

/// <summary>
/// Content search when the embedding model is down: a read never fails because a model is unavailable, so the keyword
/// index answers alone.
/// </summary>
public sealed class ContentSearchOutageTests : IAsyncLifetime
{
    private readonly AuroraGrand aurora = new(new TenantId(Guid.CreateVersion7()));
    private readonly WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Eventing:Dispatcher:Enabled", "false");
        builder.UseSetting("Inference:Providers:down:Adapter", "Fake");
        builder.UseSetting("Inference:Providers:down:Settings:Unavailable", "true");
        builder.UseSetting("Inference:Tasks:content.embed:Provider", "down");
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(AuroraGrand.Now)));
    });

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await aurora.SaveAsync(factory.Services, Cancellation);

        // Only the keyword index can be filled: indexing embeds first, and the model is down.
        ISearchIndex index = factory.Services.GetRequiredService<IBindingResolver<ISearchIndex>>().Resolve(new BindingContext("content", aurora.TenantId.Value.ToString()));
        await index.UpsertAsync(
            "content",
            new SearchDocument(aurora.WiFi.Id.Value.ToString(), aurora.TenantId, aurora.PropertyId.Value.ToString(), "Faq", aurora.WiFi.Details.Title, aurora.WiFi.Details.Body, aurora.WiFi.Details.Tags ?? []),
            Cancellation);
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task SearchContent_WhenTheQueryCannotBeEmbedded_ReturnsTheKeywordHits()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(aurora.TenantId);
        scope.ServiceProvider.GetRequiredService<OperationContext>().ActAs(new Actor(ActorType.Guest, "guest-1"));

        Result<CapabilityResponse> result = await scope.ServiceProvider.GetRequiredService<ICapabilityExecutor>().ExecuteAsync(
            new CapabilityRequest("Property.SearchContent", JsonSerializer.SerializeToElement(new { propertyId = aurora.PropertyId, query = "wifi" }, ContractJson.Options)),
            Cancellation);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        SearchContentOutput output = result.Value.Output.Deserialize<SearchContentOutput>(ContractJson.Options)!;
        Assert.Equal(["Wi-Fi"], output.Results.Select(entry => entry.Title));
    }
}
