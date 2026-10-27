using HospitalityOS.Adapters.InMemory;
using HospitalityOS.Domain.Properties.ContentItems;
using HospitalityOS.IntegrationContracts;
using HospitalityOS.IntegrationContracts.Cms;
using HospitalityOS.IntegrationContracts.Providers;
using HospitalityOS.Integrations.Native;
using HospitalityOS.PortContractTests.Integrations;
using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainContentType = HospitalityOS.Domain.Properties.ContentItems.ContentType;

namespace HospitalityOS.PortContractTests.Adapters.Integrations;

/// <summary>
/// NativeCms over in-memory persistence: the platform's own published content items.
/// </summary>
public sealed class NativeCmsConnectorContractTests : CmsConnectorContractTests, IAsyncDisposable
{
    private static readonly DateTimeOffset now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider services = BuildServices();

    public ValueTask DisposeAsync() => services.DisposeAsync();

    protected override async Task<ICmsConnector> CreateConnectorAsync(PropertyId propertyId, IReadOnlyList<CmsSeed> content)
    {
        await InScopeAsync(async scope =>
        {
            foreach (CmsSeed seed in content)
            {
                ContentItem item = ContentItem.Create(
                    new ContentItemId(Guid.CreateVersion7()),
                    ConnectorTestData.Tenant,
                    seed.PropertyId,
                    new ContentItemDetails(Enum.Parse<DomainContentType>(seed.Type.ToString()), seed.Title, seed.Body, Tags: ValueList.From(seed.Tags)),
                    now).Value;

                if (seed.Published)
                {
                    item.Publish(now);
                }

                await scope.GetRequiredService<IContentItemRepository>().AddAsync(item, Cancellation);
            }
        });

        IIntegrationProvider<ICmsConnector> provider = Assert.Single(services.GetServices<IIntegrationProvider<ICmsConnector>>());

        return provider.Create(new ConnectorContext(ConnectorTestData.Tenant, propertyId, new Dictionary<string, string>()));
    }

    protected override Task RetitleAsync(ExternalId id, string title) =>
        InScopeAsync(async scope =>
        {
            ContentItem item = (await scope.GetRequiredService<IContentItemRepository>().GetByIdAsync(new ContentItemId(Guid.Parse(id.Value)), Cancellation))!;
            Assert.True(item.Update(item.Details with { Title = title }, now).IsSuccess);
        });

    private async Task InScopeAsync(Func<IServiceProvider, Task> change)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(ConnectorTestData.Tenant);
        await change(scope.ServiceProvider);
        Result<CommitReceipt> commit = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
        Assert.True(commit.IsSuccess, commit.IsFailure ? commit.Error.Message : null);
    }

    private static ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        services.AddScoped<TenantContext>();
        services.AddScoped<ICurrentTenant>(scoped => scoped.GetRequiredService<TenantContext>());
        services.AddScoped<IEventMessageFactory, TestEventMessageFactory>();
        services.AddInMemoryPersistence();
        services.AddNativeIntegrations();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
