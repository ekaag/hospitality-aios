using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.Notifications;
using HospitalityOS.Domain.Guests.Notifications.Events;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.UnitTests.Domain.Guests.TestDoubles;
using Xunit;

namespace HospitalityOS.UnitTests.Domain.Guests.Notifications;

public sealed class GuestNotificationTests
{
    private static readonly GuestNotificationId id = new(GuestFixture.Id(50));
    private static readonly Guid towels = GuestFixture.Id(51);

    [Fact]
    public void Request_IsForTheConversationsGuestAndChannel_AndRaisesRequested()
    {
        Conversation conversation = GuestFixture.Conversation();

        GuestNotification notification = Request(conversation);

        Assert.Equal(
            (GuestNotificationStatus.Requested, "service-request-completed:1", "Your towels are here.", conversation.Channel),
            (notification.Status, notification.Key, notification.Body, notification.Channel));
        Assert.Equal(
            [new GuestNotificationRequested(id, GuestFixture.PropertyId, conversation.Id, null, conversation.Channel, "ServiceRequest", towels, GuestFixture.Now)],
            notification.DomainEvents);
    }

    [Fact]
    public void Request_WithoutAKeyOrText_ReportsBoth()
    {
        Result<GuestNotification> result = GuestNotification.Request(id, GuestFixture.Conversation(), " ", "", null, null, GuestFixture.Now);

        Assert.Equal(
            ["notification.key_required", "notification.body_required"],
            Assert.IsType<ValidationError>(result.Error).Fields.Select(field => field.Code));
    }

    [Fact]
    public void MarkSent_RecordsTheProviderOnce_AndASentNotificationCannotFail()
    {
        GuestNotification notification = Request(GuestFixture.Conversation());
        notification.DequeueDomainEvents();

        notification.MarkSent("MockMessaging", "mock-msg-000001", GuestFixture.Now);
        notification.MarkSent("MockMessaging", "mock-msg-000002", GuestFixture.Now);
        Result failed = notification.MarkFailed("integration.rejected", GuestFixture.Now);

        Assert.Equal((GuestNotificationStatus.Sent, "mock-msg-000001", "notification.final"), (notification.Status, notification.ProviderMessageId, failed.Error.Code));
        Assert.Equal([new GuestNotificationSent(id, notification.ConversationId, "MockMessaging", "mock-msg-000001", GuestFixture.Now)], notification.DomainEvents);
    }

    [Fact]
    public void MarkFailed_RecordsWhyOnce_AndAFailedNotificationCannotBeSent()
    {
        GuestNotification notification = Request(GuestFixture.Conversation());
        notification.DequeueDomainEvents();

        notification.MarkFailed("integration.rejected", GuestFixture.Now);
        notification.MarkFailed("integration.rejected", GuestFixture.Now);
        Result sent = notification.MarkSent("MockMessaging", "mock-msg-000001", GuestFixture.Now);

        Assert.Equal((GuestNotificationStatus.Failed, "integration.rejected", "notification.final"), (notification.Status, notification.FailureReason, sent.Error.Code));
        Assert.Single(notification.DomainEvents);
    }

    private static GuestNotification Request(Conversation conversation) =>
        GuestNotification.Request(id, conversation, " service-request-completed:1 ", "Your towels are here.", "ServiceRequest", towels, GuestFixture.Now).Value;
}
