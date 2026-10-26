using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

public sealed class EventingStartupTests
{
    [Fact]
    public void Start_WithDefaultConfiguration_BindsIdGeneratorForEventIds()
    {
        using WebApplicationFactory<Program> factory = new();

        IIdGenerator ids = factory.Services.GetRequiredService<IBindingResolver<IIdGenerator>>().Resolve();

        Assert.NotEqual(ids.NewId(), ids.NewId());
    }
}
