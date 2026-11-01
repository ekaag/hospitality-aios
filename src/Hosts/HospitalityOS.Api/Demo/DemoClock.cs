using System.Globalization;

namespace HospitalityOS.Api.Demo;

/// <summary>
/// A clock for demos and browser tests (L2): with <c>Demo:Clock:Start</c> (an ISO 8601 time), the API starts at that
/// moment and runs on in real time, so a seeded "tonight" is always ahead whenever a demo or a test run happens. Only in
/// Development: set anywhere else, the API refuses to start.
/// </summary>
internal static class DemoClock
{
    public const string StartKey = "Demo:Clock:Start";

    public static IServiceCollection AddDemoClock(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (configuration[StartKey] is not { Length: > 0 } start)
        {
            return services;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException($"{StartKey} sets a demo clock, which only Development may use; remove it.");
        }

        if (!DateTimeOffset.TryParse(start, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset at))
        {
            throw new InvalidOperationException($"{StartKey} must be an ISO 8601 time, e.g. 2026-10-26T18:00:00Z, not '{start}'.");
        }

        // Registered before the modules, which only add the system clock when none is there.
        return services.AddSingleton<TimeProvider>(new ShiftedTimeProvider(at));
    }

    /// <summary>
    /// The system clock, moved so that it read <c>start</c> when it was made.
    /// </summary>
    private sealed class ShiftedTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private readonly TimeSpan shift = start - System.GetUtcNow();

        public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + shift;
    }
}
