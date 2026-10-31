using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Persistence;
using HospitalityOS.Ports.Scheduling;
using HospitalityOS.SharedKernel.Execution;
using HospitalityOS.SharedKernel.Results;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HospitalityOS.Eventing.Scheduling;

/// <summary>
/// Runs due scheduled messages: each in a scope of its own, in its tenant and flow, as its handler's agent, committing the
/// handler's changes with an inbox record keyed by handler and message, so a message delivered twice runs once (P8). A
/// failure is logged and the message dropped; scheduled work is a nudge, and the state it re-checks stays as it was.
/// </summary>
internal sealed partial class ScheduledWorkHost(
    Func<string, ScheduledScope> scopes,
    IBindingResolver<IScheduler> schedulers,
    TimeProvider timeProvider,
    ILogger<ScheduledWorkHost> logger) : IHostedService
{
    private IAsyncDisposable? subscription;

    public async Task StartAsync(CancellationToken cancellationToken) =>
        subscription = await schedulers.Resolve().SubscribeAsync(RunAsync, cancellationToken).ConfigureAwait(false);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref subscription, null) is { } stopping)
        {
            await stopping.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task RunAsync(ScheduledMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await using ScheduledScope scope = scopes(message.Handler);

            if (await scope.Inbox.HasProcessedAsync(message.Handler, message.MessageId, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            scope.Tenant.Set(message.TenantId);
            scope.Operation.JoinFlow(message.CorrelationId, message.CausationId);
            scope.Operation.ActAs(Actor.Agent(message.Handler));

            // Resolved only now, so the handler is built inside the message's tenant and flow.
            await scope.Handler().InvokeAsync(message.Payload, cancellationToken).ConfigureAwait(false);
            scope.Inbox.Record(message.Handler, message.MessageId, timeProvider.GetUtcNow());
            Result<CommitReceipt> commit = await scope.UnitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

            if (commit.IsFailure)
            {
                LogFailed(message.Handler, message.MessageId, message.CorrelationId, commit.Error.Code, null);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailed(message.Handler, message.MessageId, message.CorrelationId, "crashed", exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled {Handler} message {MessageId} (correlation {CorrelationId}) failed: {ErrorCode}")]
    private partial void LogFailed(string handler, Guid messageId, Guid correlationId, string errorCode, Exception? exception);
}
