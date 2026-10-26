using HospitalityOS.Capabilities.Definitions;

namespace HospitalityOS.UnitTests.Capabilities.TestDoubles;

/// <summary>
/// Version 2 of <see cref="RequestItemCapability"/>.
/// </summary>
internal sealed class RequestItemV2Capability : ICapability<RequestItemInput, RequestItemOutput>
{
    public static CapabilityDescriptor Descriptor { get; } = RequestItemCapability.Descriptor with { Version = 2 };
}
