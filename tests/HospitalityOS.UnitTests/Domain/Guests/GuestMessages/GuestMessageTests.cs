using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.GuestMessages;
using HospitalityOS.Domain.Guests.GuestMessages.Events;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Guests.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Guests.GuestMessages;

public sealed class GuestMessageTests
{
    private static readonly GuestMessageId id = new(GuestFixture.Id(30));

    [Fact]
    public void Record_FromGuestInLinkedConversation_RaisesReceivedWithStay()
    {
        Conversation conversation = GuestFixture.Conversation();
        StayContext stay = GuestFixture.Stay(unit: GuestFixture.Room412);
        conversation.LinkStay(stay, GuestFixture.Now);

        GuestMessage message = GuestMessage.Record(id, conversation, MessageSender.Guest, "  Send two towels to my room.  ", "web-123", GuestFixture.Now).Value;

        Assert.Equal("Send two towels to my room.", message.Text);
        Assert.Equal("web-123", message.ClientMessageId);
        Assert.Equal(stay.Id, message.StayContextId);
        Assert.Equal(
            [new GuestMessageReceived(id, conversation.Id, GuestFixture.PropertyId, stay.GuestId, stay.Id, GuestFixture.Now)],
            message.DomainEvents);
    }

    [Fact]
    public void Record_FromGuestInAnonymousConversation_HasNoGuestOrStay()
    {
        GuestMessage message = GuestMessage.Record(id, GuestFixture.Conversation(), MessageSender.Guest, "What time does the pool close tonight?", null, GuestFixture.Now).Value;

        GuestMessageReceived received = Assert.IsType<GuestMessageReceived>(Assert.Single(message.DomainEvents));
        Assert.Null(received.GuestId);
        Assert.Null(received.StayContextId);
    }

    [Theory]
    [InlineData(MessageSender.Concierge)]
    [InlineData(MessageSender.Staff)]
    [InlineData(MessageSender.System)]
    public void Record_FromOthers_RaisesSent(MessageSender sender)
    {
        Conversation conversation = GuestFixture.Conversation();

        GuestMessage message = GuestMessage.Record(id, conversation, sender, "The pool closes at 10 pm tonight.", null, GuestFixture.Now).Value;

        Assert.Equal([new GuestMessageSent(id, conversation.Id, GuestFixture.PropertyId, sender, GuestFixture.Now)], message.DomainEvents);
    }

    [Theory]
    [InlineData(AnswerOutcome.Answered)]
    [InlineData(AnswerOutcome.NoAuthoritativeSource)]
    public void RecordAnswer_IsTheConciergesMessage_RaisingSentThenQuestionAnswered(AnswerOutcome outcome)
    {
        Conversation conversation = GuestFixture.Conversation();
        GuestMessage question = GuestMessage.Record(id, conversation, MessageSender.Guest, "What time does the pool close tonight?", null, GuestFixture.Now).Value;
        GuestMessageId answerId = new(GuestFixture.Id(31));

        GuestMessage answer = GuestMessage.RecordAnswer(answerId, conversation, question, " The pool closes at 22:00. ", outcome, GuestFixture.Now).Value;

        Assert.Equal((MessageSender.Concierge, "The pool closes at 22:00.", (string?)null), (answer.Sender, answer.Text, answer.ClientMessageId));
        Assert.Equal(
            [
                new GuestMessageSent(answerId, conversation.Id, GuestFixture.PropertyId, MessageSender.Concierge, GuestFixture.Now),
                new GuestQuestionAnswered(id, answerId, conversation.Id, GuestFixture.PropertyId, outcome, GuestFixture.Now),
            ],
            answer.DomainEvents);
    }

    [Fact]
    public void RecordAnswer_ToAnythingButAGuestsMessageInTheConversation_Throws()
    {
        Conversation conversation = GuestFixture.Conversation();
        GuestMessage concierge = GuestMessage.Record(id, conversation, MessageSender.Concierge, "Hello!", null, GuestFixture.Now).Value;
        GuestMessage elsewhere = GuestMessage.Record(id, GuestFixture.Conversation(idSuffix: 40), MessageSender.Guest, "Hello?", null, GuestFixture.Now).Value;

        Assert.Throws<ArgumentException>(() => GuestMessage.RecordAnswer(new GuestMessageId(GuestFixture.Id(31)), conversation, concierge, "Hi", AnswerOutcome.Answered, GuestFixture.Now));
        Assert.Throws<ArgumentException>(() => GuestMessage.RecordAnswer(new GuestMessageId(GuestFixture.Id(31)), conversation, elsewhere, "Hi", AnswerOutcome.Answered, GuestFixture.Now));
    }

    [Fact]
    public void RecordAnswer_WithoutTextOrInClosedConversation_IsRefused()
    {
        Conversation conversation = GuestFixture.Conversation();
        GuestMessage question = GuestMessage.Record(id, conversation, MessageSender.Guest, "Hello?", null, GuestFixture.Now).Value;
        GuestMessageId answerId = new(GuestFixture.Id(31));

        Result<GuestMessage> empty = GuestMessage.RecordAnswer(answerId, conversation, question, " ", AnswerOutcome.Answered, GuestFixture.Now);
        conversation.Close(GuestFixture.Now);
        Result<GuestMessage> closed = GuestMessage.RecordAnswer(answerId, conversation, question, "Hi", AnswerOutcome.Answered, GuestFixture.Now);

        Assert.Equal(("message.text_required", "conversation.closed"), (Assert.Single(Assert.IsType<ValidationError>(empty.Error).Fields).Code, closed.Error.Code));
    }

    [Fact]
    public void Triage_RecordsTheIntentOnce_RaisingTriaged()
    {
        Conversation conversation = GuestFixture.Conversation();
        StayContext stay = GuestFixture.Stay(unit: GuestFixture.Room412);
        conversation.LinkStay(stay, GuestFixture.Now);
        GuestMessage message = GuestMessage.Record(id, conversation, MessageSender.Guest, "Can you send two towels?", null, GuestFixture.Now).Value;
        message.DequeueDomainEvents();

        Result first = message.Triage(MessageIntent.ServiceRequest, GuestFixture.Now);
        Result again = message.Triage(MessageIntent.ServiceRequest, GuestFixture.Now);
        Result other = message.Triage(MessageIntent.Question, GuestFixture.Now);

        Assert.True(first.IsSuccess && again.IsSuccess);
        Assert.Equal("message.already_triaged", other.Error.Code);
        Assert.Equal(MessageIntent.ServiceRequest, message.Intent);
        Assert.Equal(
            [new GuestMessageTriaged(id, conversation.Id, GuestFixture.PropertyId, stay.GuestId, stay.Id, MessageIntent.ServiceRequest, GuestFixture.Now)],
            message.DomainEvents);
    }

    [Fact]
    public void Triage_OfAnythingButAGuestsMessage_OrAnUnknownIntent_IsRefused()
    {
        Conversation conversation = GuestFixture.Conversation();
        GuestMessage concierge = GuestMessage.Record(id, conversation, MessageSender.Concierge, "Hello!", null, GuestFixture.Now).Value;
        GuestMessage guest = GuestMessage.Record(id, conversation, MessageSender.Guest, "Hello?", null, GuestFixture.Now).Value;

        Assert.Equal("message.not_from_guest", concierge.Triage(MessageIntent.Question, GuestFixture.Now).Error.Code);
        Assert.Equal("message.intent_invalid", Assert.Single(Assert.IsType<ValidationError>(guest.Triage((MessageIntent)99, GuestFixture.Now).Error).Fields).Code);
        Assert.Null(guest.Intent);
    }

    [Fact]
    public void Record_InClosedConversation_ReturnsInvalidState()
    {
        Conversation conversation = GuestFixture.Conversation();
        conversation.Close(GuestFixture.Now);

        Result<GuestMessage> result = GuestMessage.Record(id, conversation, MessageSender.Guest, "Hello?", null, GuestFixture.Now);

        Assert.Equal("conversation.closed", result.Error.Code);
    }

    [Fact]
    public void Record_WithInvalidFields_ReportsAll()
    {
        Result<GuestMessage> result = GuestMessage.Record(
            id, GuestFixture.Conversation(), (MessageSender)99, " ", new string('x', GuestMessage.MaxClientMessageIdLength + 1), GuestFixture.Now);

        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(
            [("text", "message.text_required"), ("clientMessageId", "message.client_message_id_too_long"), ("sender", "message.sender_invalid")],
            error.Fields.Select(field => (field.Field, field.Code)));
    }

    [Fact]
    public void Record_WithTooLongText_ReturnsValidationError()
    {
        Result<GuestMessage> result = GuestMessage.Record(
            id, GuestFixture.Conversation(), MessageSender.Guest, new string('x', GuestMessage.MaxTextLength + 1), null, GuestFixture.Now);

        Assert.Equal("message.text_too_long", Assert.Single(Assert.IsType<ValidationError>(result.Error).Fields).Code);
    }
}
