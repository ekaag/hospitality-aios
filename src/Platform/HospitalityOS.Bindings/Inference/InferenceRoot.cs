using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings.Inference;

/// <summary>
/// The <c>Inference</c> configuration section, registered once by <c>AddInference</c>.
/// </summary>
/// <param name="Section">The section holding providers, task routes, and tenant overrides.</param>
internal sealed record InferenceRoot(IConfigurationSection Section);
