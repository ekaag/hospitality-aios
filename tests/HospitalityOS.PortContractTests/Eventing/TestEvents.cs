using HospitalityOS.PortContractTests.Persistence.Support;
using HospitalityOS.Ports.Eventing;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.PortContractTests.Eventing;

/// <summary>
/// Event messages for store and transport tests. The envelope text is opaque to these ports, so any JSON will do.
/// </summary>
internal static class TestEvents
{
    public static Guid FlowA { get; } = TestData.Id(900_001);

    public static Guid FlowB { get; } = TestData.Id(900_002);

    public static EventMessage Message(int seed, TenantId? tenantId = null, Guid? correlationId = null, DateTimeOffset? recordedAt = null) => new(
        TestData.Id(800_000 + seed),
        "HumanActionCompleted",
        1,
        tenantId ?? TestData.TenantA,
        "HumanAction",
        TestData.Id(700_000 + seed),
        correlationId ?? FlowA,
        recordedAt ?? TestData.Now.AddSeconds(seed),
        $$"""{"seed":{{seed}}}""");
}
