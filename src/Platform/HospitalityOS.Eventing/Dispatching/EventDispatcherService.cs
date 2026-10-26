using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HospitalityOS.Eventing.Dispatching;

/// <summary>
/// Runs the <see cref="OutboxDispatcher"/> for the life of the host: back-to-back while full batches keep coming,
/// otherwise every <see cref="EventDispatcherOptions.PollInterval"/>. A failing pass (e.g. the database is down) is
/// logged and retried on the next interval; it never stops the host.
/// </summary>
internal sealed partial class EventDispatcherService(
    OutboxDispatcher dispatcher,
    IOptions<EventDispatcherOptions> options,
    TimeProvider timeProvider,
    ILogger<EventDispatcherService> logger) : BackgroundService
{
    private readonly EventDispatcherOptions settings = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            LogDisabled();

            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            int claimed;

            try
            {
                claimed = await dispatcher.DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // Failed processing is visible, never silent (P11), and the next pass tries again.
                LogPassFailed(exception);
                claimed = 0;
            }

            if (claimed < settings.BatchSize)
            {
                await Task.Delay(settings.PollInterval, timeProvider, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Event dispatcher is disabled on this host")]
    private partial void LogDisabled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Event dispatch pass failed; retrying on the next interval")]
    private partial void LogPassFailed(Exception exception);
}
