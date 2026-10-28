using HospitalityOS.Ports.Inference;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// Routes inference tasks to providers (P6), falling back in order when a provider is unavailable. The tenant comes from
/// the scope; with none (e.g. an evaluation run) the task's default route applies.
/// </summary>
internal sealed class InferenceRouter(InferenceCatalog catalog, TenantContext tenant, TimeProvider timeProvider) : IInferenceRouter
{
    public async Task<Result<InferenceOutcome<TResult>>> RunAsync<TPort, TResult>(
        string task,
        Func<TPort, ModelSelection, CancellationToken, Task<TResult>> call,
        CancellationToken cancellationToken)
        where TPort : class
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(call);

        InferenceTaskRegistration registration = catalog.FindTask(task)
            ?? throw new InvalidOperationException($"Inference task '{task}' is not declared; declare it with AddInferenceTask<{typeof(TPort).Name}>(\"{task}\").");

        if (registration.Port != typeof(TPort))
        {
            throw new InvalidOperationException($"Inference task '{task}' is declared for {registration.Port.Name}, not {typeof(TPort).Name}.");
        }

        TaskRoute route = catalog.Configuration.Select(registration.Name, tenant.IsSet ? tenant.TenantId.Value.ToString() : null)
            ?? throw new BindingException($"Inference task '{task}' has no route; add '{InferenceConfiguration.SectionName}:{InferenceConfiguration.TasksKey}:{task}'.");
        List<InferenceAttempt> attempts = [];

        foreach (RouteTarget target in route.Targets)
        {
            TPort port = (TPort)catalog.Instance(target.Provider, typeof(TPort));
            long started = timeProvider.GetTimestamp();

            try
            {
                TResult value = await call(port, new ModelSelection(target.Model, route.Parameters), cancellationToken).ConfigureAwait(false);
                attempts.Add(new InferenceAttempt(registration.Name, target.Provider, target.Model, timeProvider.GetElapsedTime(started)));

                return new InferenceOutcome<TResult>(value, ValueList.From(attempts));
            }
            catch (InferenceUnavailableException exception)
            {
                attempts.Add(new InferenceAttempt(registration.Name, target.Provider, target.Model, timeProvider.GetElapsedTime(started), exception.Message));
            }
        }

        return new InferenceUnavailableError(registration.Name, ValueList.From(attempts));
    }
}
