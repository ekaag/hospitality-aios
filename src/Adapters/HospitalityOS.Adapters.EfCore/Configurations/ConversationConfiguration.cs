using HospitalityOS.Domain.Guests.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalityOS.Adapters.EfCore.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.MapAggregate("Conversations");
        builder.Property(conversation => conversation.PropertyId);
        builder.Property(conversation => conversation.Channel);
        builder.Property(conversation => conversation.StartedAt);
        builder.Property(conversation => conversation.GuestId);
        builder.Property(conversation => conversation.StayContextId);
        builder.Property(conversation => conversation.Status);
    }
}
