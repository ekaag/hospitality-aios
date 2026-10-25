using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.Conversations.Events;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Guests.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Guests.Conversations;

public sealed class ConversationTests
{
    private static readonly ConversationId id = new(GuestFixture.Id(20));

    [Fact]
    public void Start_OpensAnonymousConversationAndRaisesStarted()
    {
        Conversation conversation = Conversation.Start(id, GuestFixture.TenantId, GuestFixture.PropertyId, ConversationChannel.Web, GuestFixture.Now).Value;

        Assert.Equal(ConversationStatus.Open, conversation.Status);
        Assert.Null(conversation.GuestId);
        Assert.Null(conversation.StayContextId);
        Assert.Equal([new ConversationStarted(id, GuestFixture.PropertyId, ConversationChannel.Web, GuestFixture.Now)], conversation.DomainEvents);
    }

    [Fact]
    public void Start_WithUnknownChannel_ReturnsValidationError()
    {
        Result<Conversation> result = Conversation.Start(id, GuestFixture.TenantId, GuestFixture.PropertyId, (ConversationChannel)99, GuestFixture.Now);

        Assert.Equal("conversation.channel_invalid", Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }

    [Fact]
    public void LinkStay_IdentifiesGuestAndRaisesUpdated_AndRelinkingIsNoOp()
    {
        Conversation conversation = GuestFixture.Conversation();
        StayContext stay = GuestFixture.Stay();

        conversation.LinkStay(stay, GuestFixture.Now);
        conversation.LinkStay(stay, GuestFixture.Now.AddMinutes(1));

        Assert.Equal(stay.GuestId, conversation.GuestId);
        Assert.Equal(stay.Id, conversation.StayContextId);
        Assert.Equal([new ConversationUpdated(id, stay.GuestId, stay.Id, ConversationStatus.Open, GuestFixture.Now)], conversation.DomainEvents);
    }

    [Fact]
    public void LinkStay_AtOtherProperty_ReturnsValidationError()
    {
        Conversation conversation = GuestFixture.Conversation();

        Result result = conversation.LinkStay(GuestFixture.Stay(propertyId: GuestFixture.OtherPropertyId), GuestFixture.Now);

        Assert.Equal("conversation.stay_other_property", Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }

    [Fact]
    public void LinkStay_OfDifferentGuest_ReturnsValidationError()
    {
        Conversation conversation = GuestFixture.Conversation();
        conversation.LinkStay(GuestFixture.Stay(), GuestFixture.Now);
        Guest otherGuest = GuestFixture.Guest(idSuffix: 2);

        Result result = conversation.LinkStay(GuestFixture.Stay(otherGuest, idSuffix: 11), GuestFixture.Now);

        Assert.Equal("conversation.stay_other_guest", Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }

    [Fact]
    public void Close_Twice_RaisesOnce_AndBlocksLinking()
    {
        Conversation conversation = GuestFixture.Conversation();

        conversation.Close(GuestFixture.Now);
        conversation.Close(GuestFixture.Now.AddMinutes(1));
        Result link = conversation.LinkStay(GuestFixture.Stay(), GuestFixture.Now);

        Assert.Single(conversation.DomainEvents);
        Assert.Equal("conversation.closed", link.Error.Code);
    }
}
