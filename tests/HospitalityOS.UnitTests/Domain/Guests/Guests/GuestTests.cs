using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.Guests.Events;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Guests.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Guests.Guests;

public sealed class GuestTests
{
    private static readonly GuestId id = new(GuestFixture.Id(1));

    [Fact]
    public void Register_RaisesRegisteredWithoutPersonalData()
    {
        Guest guest = Guest.Register(id, GuestFixture.TenantId, new GuestProfile("Alex", "en"), GuestFixture.Now).Value;

        Assert.Equal(new GuestProfile("Alex", "en"), guest.Profile);
        Assert.Equal([new GuestRegistered(id, GuestFixture.Now)], guest.DomainEvents);
    }

    [Fact]
    public void Register_Anonymous_IsAllowed()
    {
        Guest guest = Guest.Register(id, GuestFixture.TenantId, GuestProfile.Anonymous, GuestFixture.Now).Value;

        Assert.Equal(GuestProfile.Anonymous, guest.Profile);
    }

    [Theory]
    [InlineData("EN", "en")]
    [InlineData("pt_br", "pt-BR")]
    [InlineData(" fil ", "fil")]
    [InlineData("en-us", "en-US")]
    public void Register_NormalizesLanguage(string input, string expected)
    {
        Guest guest = Guest.Register(id, GuestFixture.TenantId, new GuestProfile(PreferredLanguage: input), GuestFixture.Now).Value;

        Assert.Equal(expected, guest.Profile.PreferredLanguage);
    }

    [Theory]
    [InlineData("english")]
    [InlineData("e")]
    [InlineData("en-USA")]
    [InlineData("en-US-x")]
    [InlineData("e1")]
    public void Register_WithInvalidLanguage_ReturnsValidationError(string input)
    {
        Result<Guest> result = Guest.Register(id, GuestFixture.TenantId, new GuestProfile(PreferredLanguage: input), GuestFixture.Now);

        Assert.Equal("guest.preferred_language_invalid", Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }

    [Fact]
    public void UpdateProfile_WithChange_RaisesUpdated_AndSameProfileIsNoOp()
    {
        Guest guest = GuestFixture.Guest();

        guest.UpdateProfile(new GuestProfile("Alex R.", "en"), GuestFixture.Now.AddHours(1));
        guest.UpdateProfile(new GuestProfile(" Alex R. ", "EN"), GuestFixture.Now.AddHours(2));

        Assert.Equal([new GuestProfileUpdated(guest.Id, GuestFixture.Now.AddHours(1))], guest.DomainEvents);
    }
}
