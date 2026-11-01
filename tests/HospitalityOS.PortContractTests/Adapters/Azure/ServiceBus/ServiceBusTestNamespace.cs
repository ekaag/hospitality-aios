using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Xunit;

namespace HospitalityOS.PortContractTests.Adapters.Azure.ServiceBus;

/// <summary>
/// The Service Bus namespace <see cref="ConnectionVariable"/> names (CI runs the emulator; locally, start it with
/// <c>tests/emulators/servicebus</c> and set the variables), where each test makes its own topic or queue and deletes it
/// afterwards. In production, entities come from IaC; only tests create them. Without the variable, tests are skipped.
/// </summary>
internal sealed class ServiceBusTestNamespace
{
    /// <summary>
    /// The connection string the adapters send and receive with, e.g. the emulator's
    /// <c>Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;</c>.
    /// </summary>
    public const string ConnectionVariable = "HOSPITALITYOS_SERVICEBUS";

    /// <summary>
    /// The connection string for creating and deleting entities, when it differs: the emulator serves management on its
    /// own port (<c>sb://localhost:5300</c>).
    /// </summary>
    private const string AdministrationVariable = "HOSPITALITYOS_SERVICEBUS_ADMIN";

    /// <summary>
    /// Set where a namespace must be there (CI), so a missing one fails the tests instead of skipping them.
    /// </summary>
    private const string RequiredVariable = "HOSPITALITYOS_SERVICEBUS_REQUIRED";

    /// <summary>
    /// Long enough for any test, and within the emulator's limit of one hour.
    /// </summary>
    private static readonly TimeSpan timeToLive = TimeSpan.FromHours(1);

    /// <summary>
    /// The broker's own limit, above any the adapters are configured with, so the adapters' limits are what the tests see.
    /// </summary>
    private const int BrokerMaxDeliveryCount = 10;

    private readonly Lazy<ServiceBusAdministrationClient> administration;

    public ServiceBusTestNamespace()
    {
        administration = new Lazy<ServiceBusAdministrationClient>(() => new ServiceBusAdministrationClient(
            Environment.GetEnvironmentVariable(AdministrationVariable) is { Length: > 0 } admin ? admin : ConnectionString));
    }

    public string ConnectionString { get; } = Environment.GetEnvironmentVariable(ConnectionVariable) ?? string.Empty;

    private static bool Required => string.Equals(Environment.GetEnvironmentVariable(RequiredVariable), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Skips the test when no namespace is set, unless one is required.
    /// </summary>
    public void SkipUnlessAvailable()
    {
        Assert.SkipWhen(string.IsNullOrWhiteSpace(ConnectionString) && !Required, $"Set {ConnectionVariable} to a Service Bus namespace to run the Service Bus contract tests.");
        Assert.False(string.IsNullOrWhiteSpace(ConnectionString), $"{RequiredVariable} is set, so {ConnectionVariable} must name a Service Bus namespace.");
    }

    public async Task CreateTopicAsync(string topic, CancellationToken cancellationToken) =>
        await administration.Value.CreateTopicAsync(new CreateTopicOptions(topic) { DefaultMessageTimeToLive = timeToLive }, cancellationToken);

    /// <summary>
    /// Creates the subscription unless it is already there.
    /// </summary>
    public async Task EnsureSubscriptionAsync(string topic, string subscription, CancellationToken cancellationToken)
    {
        try
        {
            await administration.Value.CreateSubscriptionAsync(
                new CreateSubscriptionOptions(topic, subscription) { DefaultMessageTimeToLive = timeToLive, MaxDeliveryCount = BrokerMaxDeliveryCount },
                cancellationToken);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
        }
    }

    public async Task CreateQueueAsync(string queue, CancellationToken cancellationToken) =>
        await administration.Value.CreateQueueAsync(new CreateQueueOptions(queue) { DefaultMessageTimeToLive = timeToLive, MaxDeliveryCount = BrokerMaxDeliveryCount }, cancellationToken);

    public async Task DeleteTopicAsync(string topic) => await IgnoreMissingAsync(() => administration.Value.DeleteTopicAsync(topic));

    public async Task DeleteQueueAsync(string queue) => await IgnoreMissingAsync(() => administration.Value.DeleteQueueAsync(queue));

    private static async Task IgnoreMissingAsync(Func<Task<Response>> delete)
    {
        try
        {
            await delete();
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
        }
    }
}
