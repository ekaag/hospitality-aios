using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Execution;

/// <summary>
/// Runs one registered capability on input already read into its input type, so the executor needs no generics.
/// Keyed by <see cref="Definitions.CapabilityDefinition.Key"/>; scoped.
/// </summary>
internal interface ICapabilityInvoker
{
    Task<Result<object?>> InvokeAsync(object input, CancellationToken cancellationToken);
}
