using HospitalityOS.SharedKernel.Ids;

namespace HospitalityOS.UnitTests.SharedKernel.TestDoubles;

/// <summary>
/// A strongly typed ID shaped the way domain projects define theirs.
/// </summary>
internal readonly record struct SampleId : ITypedId
{
    public SampleId(Guid value)
    {
        Value = TypedIds.EnsureNotEmpty(value, nameof(SampleId));
    }

    public Guid Value { get; }
}
