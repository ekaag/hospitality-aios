using HospitalityOS.Domain.Guests.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class GuestNotificationConfiguration : IEntityTypeConfiguration<GuestNotification>
{
    public void Configure(EntityTypeBuilder<GuestNotification> builder)
    {
        builder.MapAggregate("GuestNotifications");
        builder.Property(notification => notification.PropertyId);
        builder.Property(notification => notification.ConversationId);
        builder.Property(notification => notification.GuestId);
        builder.Property(notification => notification.Channel);
        builder.Property(notification => notification.Key).HasMaxLength(GuestNotification.MaxKeyLength);
        builder.Property(notification => notification.Body);
        builder.Property(notification => notification.SubjectType);
        builder.Property(notification => notification.SubjectId);
        builder.Property(notification => notification.RequestedAt);
        builder.Property(notification => notification.Status);
        builder.Property(notification => notification.ProviderName);
        builder.Property(notification => notification.ProviderMessageId);
        builder.Property(notification => notification.SentAt);
        builder.Property(notification => notification.FailureReason);

        // A key is one logical message; two deliveries racing to send it cannot both commit.
        builder.HasIndex(notification => notification.Key).IsUnique();
    }
}
