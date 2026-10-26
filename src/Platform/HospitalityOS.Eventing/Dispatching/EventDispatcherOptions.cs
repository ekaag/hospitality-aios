namespace HospitalityOS.Eventing.Dispatching;

/// <summary>
/// How the outbox dispatcher runs; configuration section <c>Eventing:Dispatcher</c>.
/// </summary>
public sealed class EventDispatcherOptions
{
    public const string SectionName = "Eventing:Dispatcher";

    /// <summary>
    /// Whether this host runs a dispatcher. Several hosts may run one; leases keep them from sharing an event.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long to wait after finding fewer due events than a full batch.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The most events claimed at once.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// How long a claim lasts; must comfortably exceed the time to publish a batch.
    /// </summary>
    public TimeSpan Lease { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Publish attempts before an event is dead-lettered (spec §37).
    /// </summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// Wait before the first retry; doubles with each further failure.
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The longest wait between retries.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    internal IEnumerable<string> Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            yield return $"{SectionName}:PollInterval must be positive.";
        }

        if (BatchSize < 1)
        {
            yield return $"{SectionName}:BatchSize must be at least 1.";
        }

        if (Lease <= TimeSpan.Zero)
        {
            yield return $"{SectionName}:Lease must be positive.";
        }

        if (MaxAttempts < 1)
        {
            yield return $"{SectionName}:MaxAttempts must be at least 1.";
        }

        if (InitialRetryDelay <= TimeSpan.Zero || MaxRetryDelay < InitialRetryDelay)
        {
            yield return $"{SectionName}:InitialRetryDelay must be positive and no more than MaxRetryDelay.";
        }
    }
}
