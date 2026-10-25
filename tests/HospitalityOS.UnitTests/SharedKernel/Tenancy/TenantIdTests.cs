using HospitalityOS.SharedKernel.Tenancy;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Tenancy;

public sealed class TenantIdTests
{
    private static readonly Guid value = Guid.Parse("01890a5d-ac96-774b-bcce-b302099a8057");

    [Fact]
    public void Equals_WithSameValue_IsTrue()
    {
        TenantId first = new(value);
        TenantId second = new(value);

        Assert.Equal(first, second);
        Assert.True(first == second);
    }

    [Fact]
    public void Constructor_WithEmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));
    }

    [Fact]
    public void ToString_ReturnsUnderlyingGuid()
    {
        TenantId tenantId = new(value);

        Assert.Equal(value.ToString(), tenantId.ToString());
    }
}
