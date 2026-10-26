using System.ComponentModel;
using HospitalityOS.SharedKernel.Collections;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Pricing;

namespace HospitalityOS.UnitTests.Capabilities.TestDoubles;

[Description("A guest's request for an item.")]
internal sealed record RequestItemInput(
    [property: Description("The property the guest is staying at.")] PropertyId PropertyId,
    string ItemCode,
    int Quantity,
    string? Note,
    SpaceId? DeliverTo,
    ValueList<string> Tags,
    RequestUrgency Urgency,
    DateTimeOffset? WantedBy,
    Money? Budget);
