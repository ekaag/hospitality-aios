using HospitalityOS.SharedKernel.Domain;

namespace HospitalityOS.UnitTests.SharedKernel.TestDoubles;

/// <summary>
/// A second aggregate type sharing <see cref="SampleId"/>, to prove equality also compares type.
/// </summary>
internal sealed class OtherSampleAggregate : AggregateRoot<SampleId>
{
    public OtherSampleAggregate(SampleId id)
        : base(id)
    {
    }
}
