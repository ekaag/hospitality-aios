using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.PortContractTests.Persistence;

/// <summary>
/// Base of the persistence contract suites. Each persistence adapter subclasses every suite and registers itself
/// in <see cref="AddPersistence"/>; every test gets a fresh, empty store.
/// </summary>
public abstract class PersistenceContractTests : IAsyncDisposable
{
    private ServiceProvider? provider;

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        if (provider is not null)
        {
            await provider.DisposeAsync();
        }

        await DisposeStoreAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Registers the adapter under test: the unit of work and every repository.
    /// </summary>
    protected abstract void AddPersistence(IServiceCollection services);

    /// <summary>
    /// Prepares an empty store before the first session, e.g. applies a database's migrations.
    /// </summary>
    protected virtual void InitializeStore(IServiceProvider services)
    {
    }

    /// <summary>
    /// Releases what the store holds beyond the container, e.g. a database connection.
    /// </summary>
    protected virtual ValueTask DisposeStoreAsync() => ValueTask.CompletedTask;

    internal PersistenceSession OpenSession(TenantId? tenantId = null)
    {
        if (provider is null)
        {
            provider = BuildProvider();
            InitializeStore(provider);
        }

        AsyncServiceScope scope = provider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentTenant>().TenantId = tenantId ?? TestData.TenantA;

        return new PersistenceSession(scope);
    }

    /// <summary>
    /// Adds and commits each aggregate in its own scope for its tenant, asserting success.
    /// </summary>
    internal async Task SaveAsync(params IAggregateRoot[] aggregates)
    {
        foreach (IAggregateRoot aggregate in aggregates)
        {
            AggregateCase aggregateCase = AggregateCases.Get(aggregate.GetType().Name);
            await using PersistenceSession session = OpenSession(((ITenantScoped)aggregate).TenantId);
            await aggregateCase.AddAsync(session, aggregate, Cancellation);
            Result<CommitReceipt> commit = await session.CommitAsync(Cancellation);
            Assert.True(commit.IsSuccess, commit.IsFailure ? commit.Error.Message : null);
        }
    }

    private ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddScoped<TestCurrentTenant>();
        services.AddScoped<ICurrentTenant>(scoped => scoped.GetRequiredService<TestCurrentTenant>());
        services.AddScoped<IEventMessageFactory, TestEventMessageFactory>();
        AddPersistence(services);

        // Same validation as the hosts (P15), so a registration mistake in the adapter fails here.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
