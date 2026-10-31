using HospitalityOS.Domain.Guests.GuestMessages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class GuestMessageConfiguration : IEntityTypeConfiguration<GuestMessage>
{
    public void Configure(EntityTypeBuilder<GuestMessage> builder)
    {
        builder.MapAggregate("GuestMessages");
        builder.Property(message => message.PropertyId);
        builder.Property(message => message.ConversationId);
        builder.Property(message => message.GuestId);
        builder.Property(message => message.StayContextId);
        builder.Property(message => message.Sender);
        builder.Property(message => message.Text).HasMaxLength(GuestMessage.MaxTextLength);
        builder.Property(message => message.ClientMessageId).HasMaxLength(GuestMessage.MaxClientMessageIdLength);
        builder.Property(message => message.SentAt);
        builder.Property(message => message.Intent);
        builder.HasIndex(message => new { message.ConversationId, message.ClientMessageId }).IsUnique();
        builder.HasIndex(message => new { message.ConversationId, message.SentAt });
    }
}
