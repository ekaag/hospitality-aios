namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Picks one label for a text, e.g. routing a guest message as a question, service request, status check, or
/// maintenance report.
/// </summary>
public interface IClassifier
{
    /// <summary>
    /// Classifies <see cref="ClassificationRequest.Input"/> as exactly one of the request's labels.
    /// </summary>
    /// <exception cref="InferenceUnavailableException">The provider cannot serve the call now.</exception>
    Task<Classification> ClassifyAsync(ClassificationRequest request, ModelSelection model, CancellationToken cancellationToken);
}
