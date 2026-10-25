using HospitalityOS.Adapters.Local;
using HospitalityOS.Bindings;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Local.IdGenerator;

/// <summary>
/// Exercises the GUID v7 adapter the way the host will: bound through configuration (A3).
/// </summary>
public sealed class GuidV7IdGeneratorTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewId_ReturnsVersion7Guid()
    {
        IIdGenerator generator = Resolve(new FakeTimeProvider(now));

        Guid id = generator.NewId();

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(7, id.Version);
    }

    [Fact]
    public void NewId_EmbedsCurrentTimeFromTimeProvider()
    {
        IIdGenerator generator = Resolve(new FakeTimeProvider(now));

        Guid id = generator.NewId();

        // GUID v7 starts with the 48-bit Unix timestamp in milliseconds (RFC 9562).
        long milliseconds = Convert.ToInt64(id.ToString("N")[..12], 16);
        Assert.Equal(now.ToUnixTimeMilliseconds(), milliseconds);
    }

    [Fact]
    public void NewId_CalledRepeatedly_ReturnsUniqueIds()
    {
        IIdGenerator generator = Resolve(new FakeTimeProvider(now));

        HashSet<Guid> ids = [.. Enumerable.Range(0, 1000).Select(_ => generator.NewId())];

        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void NewId_AsTimeAdvances_SortsByCreationTime()
    {
        FakeTimeProvider timeProvider = new(now);
        IIdGenerator generator = Resolve(timeProvider);

        Guid earlier = generator.NewId();
        timeProvider.Advance(TimeSpan.FromMilliseconds(1));
        Guid later = generator.NewId();

        Assert.True(string.CompareOrdinal(earlier.ToString("N"), later.ToString("N")) < 0);
    }

    private static IIdGenerator Resolve(TimeProvider timeProvider)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Bindings:IdGenerator:Default:Adapter"] = "GuidV7" })
            .Build();
        ServiceCollection services = new();

        services.AddSingleton(timeProvider);
        services.AddBindings(configuration);
        services.AddPortBinding<IIdGenerator>();
        services.AddLocalAdapters();

        ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        return provider.GetRequiredService<IBindingResolver<IIdGenerator>>().Resolve(BindingContext.Default);
    }
}
