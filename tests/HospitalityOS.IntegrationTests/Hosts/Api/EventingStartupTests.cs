using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Eventing;
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

    [Fact]
    public async Task Start_WithDefaultConfiguration_DeliversEventsThroughTheBoundTransport()
    {
        using WebApplicationFactory<Program> factory = new();
        IEventTransport transport = factory.Services.GetRequiredService<IBindingResolver<IEventTransport>>().Resolve();
        TaskCompletionSource<Guid> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using IAsyncDisposable subscription = await transport.SubscribeAsync(
            "startup-probe",
            (message, _) =>
            {
                received.TrySetResult(message.EventId);

                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);
        Guid eventId = Guid.CreateVersion7();

        await transport.PublishAsync(
            new EventMessage(eventId, "Probe", 1, new(Guid.CreateVersion7()), "Probe", Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UnixEpoch, "{}"),
            TestContext.Current.CancellationToken);

        Assert.Equal(eventId, await received.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }
}
