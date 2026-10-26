using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.UnitTests.Capabilities.TestDoubles;

internal sealed record RequestItemOutput(ServiceRequestId RequestId, ValueList<SpaceId> Route);
