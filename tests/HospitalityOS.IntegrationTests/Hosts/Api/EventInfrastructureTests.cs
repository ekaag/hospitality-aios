using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalityOS.Eventing.Handling;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// The event subscriptions IaC deploys (<c>deploy/events.json</c>) are exactly the API's event handlers: the cloud
/// transport may not create subscriptions (P12), so a handler without one would fail at startup, and a subscription
/// without a handler would collect events nobody reads.
/// </summary>
public sealed partial class EventInfrastructureTests
{
    /// <summary>
    /// Service Bus subscription names: 1 to 50 letters, digits, periods, hyphens, and underscores, starting and ending
    /// with a letter or digit.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9]([A-Za-z0-9._-]{0,48}[A-Za-z0-9])?$")]
    private static partial Regex SubscriptionName();

    [Fact]
    public void DeclaredSubscriptions_AreExactlyTheHandlers()
    {
        using WebApplicationFactory<Program> factory = new();
        string[] handlers = [.. factory.Services.GetServices<EventHandlerRegistration>().Select(registration => registration.Name).Distinct().Order(StringComparer.Ordinal)];

        string[] declared = [.. Declared().Order(StringComparer.Ordinal)];

        Assert.True(
            handlers.SequenceEqual(declared),
            $"deploy/events.json must list every event handler as a subscription. Missing: [{string.Join(", ", handlers.Except(declared))}]; not handled: [{string.Join(", ", declared.Except(handlers))}].");
    }

    [Fact]
    public void DeclaredSubscriptions_AreValidServiceBusNames_AndUnique()
    {
        string[] declared = [.. Declared()];

        Assert.All(declared, name => Assert.Matches(SubscriptionName(), name));
        Assert.Equal(declared.Length, declared.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static IEnumerable<string> Declared()
    {
        using JsonDocument events = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "deploy", "events.json")));

        return [.. events.RootElement.GetProperty("subscriptions").EnumerateArray().Select(name => name.GetString() ?? string.Empty)];
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HospitalityOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (HospitalityOS.slnx) was not found.");
    }
}
