namespace HospitalityOS.Ports.Inference;

/// <summary>
/// Schema-constrained extraction, e.g. service, item, quantity, and location from a guest's request (I1).
/// </summary>
public interface IStructuredExtractor
{
    /// <summary>
    /// Reads <see cref="ExtractionRequest.Input"/> into a JSON value shaped by the request's schema. The caller still
    /// validates the value: providers constrain output differently.
    /// </summary>
    /// <exception cref="InferenceUnavailableException">The provider cannot serve the call now.</exception>
    Task<Extraction> ExtractAsync(ExtractionRequest request, ModelSelection model, CancellationToken cancellationToken);
}
