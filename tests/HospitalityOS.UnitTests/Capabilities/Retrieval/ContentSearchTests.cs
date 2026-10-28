using HospitalityOS.Capabilities.Retrieval;
using HospitalityOS.Ports.Search;
using Xunit;

namespace HospitalityOS.UnitTests.Capabilities.Retrieval;

public sealed class ContentSearchTests
{
    [Fact]
    public void Fuse_RanksByReciprocalRankAcrossBothLists_SoAgreementBeatsOneStrongList()
    {
        SearchHit[] keyword = [new("wifi", 9), new("parking", 3), new("spa", 1)];
        SearchHit[] semantic = [new("parking", 0.9), new("pool", 0.8)];

        Assert.Equal(["parking", "wifi", "pool", "spa"], ContentSearch.Fuse(keyword, semantic));
    }

    [Fact]
    public void Fuse_OneListAlone_KeepsItsOrder_AndTiesAreStable()
    {
        SearchHit[] keyword = [new("b", 2), new("a", 1)];

        Assert.Equal(["b", "a"], ContentSearch.Fuse(keyword, []));
        Assert.Equal(["a", "b"], ContentSearch.Fuse([new("b", 1)], [new("a", 1)]));
        Assert.Empty(ContentSearch.Fuse([], []));
    }
}
