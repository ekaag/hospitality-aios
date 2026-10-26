using HospitalityOS.Capabilities.Definitions;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Capabilities.Execution;

internal sealed class CapabilityInvoker<TCapability, TInput, TOutput>(TCapability capability) : ICapabilityInvoker
    where TCapability : ICapability<TInput, TOutput>
    where TInput : class
    where TOutput : class
{
    public async Task<Result<object?>> InvokeAsync(object input, CancellationToken cancellationToken)
    {
        Result<TOutput> result = await capability.ExecuteAsync((TInput)input, cancellationToken).ConfigureAwait(false);

        return result.IsSuccess ? Result.Success<object?>(result.Value) : result.Error;
    }
}
