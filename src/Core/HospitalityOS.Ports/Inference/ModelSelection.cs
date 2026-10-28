namespace HospitalityOS.Ports.Inference;

/// <summary>
/// The model and parameters one inference call uses, chosen by <see cref="IInferenceRouter"/> from the task's binding
/// (P6): callers name a task, never a model.
/// </summary>
/// <param name="Model">The provider's model name, e.g. <c>claude-sonnet-5-5</c>; opaque to the platform.</param>
/// <param name="Parameters">Sampling and length settings.</param>
public sealed record ModelSelection(string Model, InferenceParameters Parameters);
