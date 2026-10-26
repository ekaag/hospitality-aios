using HospitalityOS.Capabilities.Definitions;

using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.UnitTests.Capabilities.TestDoubles;

/// <summary>
/// Version 2 of <see cref="RequestItemCapability"/>.
/// </summary>
internal sealed class RequestItemV2Capability : ICapability<RequestItemInput, RequestItemOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = RequestItemCapability.Descriptor with { Version = 2 };

    public Task<Result<RequestItemOutput>> ExecuteAsync(RequestItemInput input, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Declaration only.");
}
