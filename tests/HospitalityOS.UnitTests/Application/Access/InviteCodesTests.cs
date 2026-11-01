using System.Text.RegularExpressions;
using HospitalityOS.Application.Access;
using Xunit;

namespace HospitalityOS.UnitTests.Application.Access;

public sealed partial class InviteCodesTests
{
    [Fact]
    public void New_GivesARandomGroupedCode_AndTheHashItIsStoredUnder()
    {
        (string code, string hash) = InviteCodes.New();
        (string other, _) = InviteCodes.New();

        Assert.Matches(Shape(), code);
        Assert.NotEqual(code, other);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.Equal(hash, InviteCodes.Hash(code));
    }

    [Theory]
    [InlineData("k7qf3 mzp9a 4wx2c h8rtd")]
    [InlineData("K7QF3MZP9A4WX2CH8RTD")]
    [InlineData("K7QF3-MZP9A-4WX2C-H8RTD")]
    public void Hash_IgnoresCaseSpacesAndHyphens(string typed)
    {
        Assert.Equal(InviteCodes.Hash("K7QF3-MZP9A-4WX2C-H8RTD"), InviteCodes.Hash(typed));
    }

    [Fact]
    public void Hash_ReadsLettersMistakenForDigits_AsTheDigits()
    {
        Assert.Equal(InviteCodes.Hash("K7QF3-MZP9A-4WX2C-H8R10"), InviteCodes.Hash("K7QF3-MZP9A-4WX2C-H8RLO"));
        Assert.Equal(InviteCodes.Hash("K7QF3-MZP9A-4WX2C-H8R10"), InviteCodes.Hash("K7QF3-MZP9A-4WX2C-H8RIo"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7QF3-MZP9A-4WX2C-H8RT")]
    [InlineData("K7QF3-MZP9A-4WX2C-H8RTDX")]
    [InlineData("K7QF3-MZP9A-4WX2C-H8RTU")]
    [InlineData("K7QF3-MZP9A-4WX2C-H8RT!")]
    public void Hash_IsNull_ForWhatCannotBeACode(string? typed)
    {
        Assert.Null(InviteCodes.Hash(typed));
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{5}){3}$")]
    private static partial Regex Shape();
}
