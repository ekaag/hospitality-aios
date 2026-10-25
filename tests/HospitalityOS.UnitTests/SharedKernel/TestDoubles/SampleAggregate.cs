using HospitalityOS.SharedKernel.Domain;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.UnitTests.SharedKernel.TestDoubles;

/// <summary>
/// A minimal aggregate exercising the SharedKernel base types the way B2+ aggregates will.
/// </summary>
internal sealed class SampleAggregate : AggregateRoot<SampleId>, ITenantScoped
{
    public SampleAggregate(SampleId id, TenantId tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    public TenantId TenantId { get; }

    public string Name { get; private set; } = "";

    public Result Rename(string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("name", "sample.name_required", "Name is required.");
        }

        Name = name;
        Raise(new SampleRenamed(Id, name, now));

        return Result.Success();
    }
}
