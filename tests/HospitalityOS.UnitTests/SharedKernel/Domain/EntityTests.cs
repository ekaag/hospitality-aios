using HospitalityOS.SharedKernel.Tenancy;
using HospitalityOS.UnitTests.SharedKernel.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Domain;

public sealed class EntityTests
{
    private static readonly SampleId id = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057"));
    private static readonly SampleId otherId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8058"));
    private static readonly TenantId tenantId = new(Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8059"));

    [Fact]
    public void Equals_WithSameTypeAndId_IsTrue()
    {
        SampleAggregate first = new(id, tenantId);
        SampleAggregate second = new(id, tenantId);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentId_IsFalse()
    {
        SampleAggregate first = new(id, tenantId);
        SampleAggregate second = new(otherId, tenantId);

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void Equals_WithDifferentTypeAndSameId_IsFalse()
    {
        SampleAggregate first = new(id, tenantId);
        OtherSampleAggregate second = new(id);

        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Equals_WithNull_IsFalse()
    {
        SampleAggregate entity = new(id, tenantId);

        Assert.False(entity.Equals(null));
        Assert.False(entity == null);
    }

    [Fact]
    public void Constructor_WithDefaultId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SampleAggregate(default, tenantId));
    }

    [Fact]
    public void TypedIdConstructor_WithEmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SampleId(Guid.Empty));
    }
}
