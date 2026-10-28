using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Runs an inference task (P6): resolves the task's binding (the caller's tenant's override, else the default) to a
/// provider, model, and parameters, plus the task's prompt at the bound version, calls it, and on
/// <see cref="InferenceUnavailableException"/> tries the task's fallbacks in order with the same prompt. Tasks are declared
/// in code and bound in configuration (<c>Inference</c>); startup fails if a declared task is unbound or misbound. Scoped.
/// </summary>
public interface IInferenceRouter
{
    /// <summary>
    /// Runs <paramref name="task"/> through <paramref name="call"/>, which receives the port instance and model of each
    /// attempt and the task's prompt (<c>null</c> when the task declares none), which it renders into its request; every
    /// attempt records the prompt version. Exceptions other than <see cref="InferenceUnavailableException"/> are not
    /// retried and propagate.
    /// </summary>
    /// <typeparam name="TPort">The inference port the task is declared for, e.g. <see cref="IChatModel"/>.</typeparam>
    /// <typeparam name="TResult">What <paramref name="call"/> returns.</typeparam>
    /// <param name="task">The task, e.g. <c>concierge.answer</c>.</param>
    /// <param name="call">The call to make, e.g.
    /// <c>(chat, model, prompt, ct) =&gt; chat.CompleteAsync(new ChatRequest(prompt!.Render(values), messages), model, ct)</c>.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The result with every attempt made, or <see cref="InferenceUnavailableError"/> (<c>inference.unavailable</c>)
    /// when no provider could serve the task.</returns>
    /// <exception cref="InvalidOperationException">The task is not declared, or not for <typeparamref name="TPort"/>.</exception>
    Task<Result<InferenceOutcome<TResult>>> RunAsync<TPort, TResult>(
        string task,
        Func<TPort, ModelSelection, Prompt?, CancellationToken, Task<TResult>> call,
        CancellationToken cancellationToken)
        where TPort : class;
}
