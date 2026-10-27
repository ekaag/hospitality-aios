using HospitalityOS.IntegrationContracts;
using Xunit;

namespace HospitalityOS.UnitTests.IntegrationContracts;

public sealed class ExternalIdTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new ExternalId(value));
    }

    [Fact]
    public void Constructor_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ExternalId(null!));
    }

    [Fact]
    public void Constructor_LongerThanMaxLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExternalId(new string('x', ExternalId.MaxLength + 1)));
    }

    [Fact]
    public void Constructor_AtMaxLength_KeepsValue()
    {
        string value = new('x', ExternalId.MaxLength);

        ExternalId id = new(value);

        Assert.Equal(value, id.Value);
    }

    [Fact]
    public void Value_IsKeptExactlyAsIssued()
    {
        ExternalId id = new(" RES-00412/a ");

        Assert.Equal(" RES-00412/a ", id.Value);
        Assert.Equal(" RES-00412/a ", id.ToString());
    }

    [Fact]
    public void Equality_IsOrdinal()
    {
        Assert.Equal(new ExternalId("abc-1"), new ExternalId("abc-1"));
        Assert.NotEqual(new ExternalId("abc-1"), new ExternalId("ABC-1"));
    }
}
