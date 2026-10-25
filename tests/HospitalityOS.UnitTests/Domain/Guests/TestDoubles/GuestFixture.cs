using HospitalityOS.Domain.Guests.Conversations;
using HospitalityOS.Domain.Guests.Guests;
using HospitalityOS.Domain.Guests.StayContexts;
using HospitalityOS.SharedKernel.Ids;
using HospitalityOS.SharedKernel.Tenancy;

namespace HospitalityOS.UnitTests.Domain.Guests.TestDoubles;

/// <summary>
/// A guest staying in Room 412 at Aurora Grand, and helpers to build their stay and conversation.
/// </summary>
internal static class GuestFixture
{
    public static TenantId TenantId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209912001"));

    public static PropertyId PropertyId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209912002"));

    public static PropertyId OtherPropertyId { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209912003"));

    public static AccommodationUnitId Room412 { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209912004"));

    public static AccommodationUnitId Room415 { get; } = new(Guid.Parse("01890a5d-ac96-774b-bcce-b30209912005"));

    public static DateTimeOffset Now { get; } = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static DateOnly Arrival { get; } = new(2026, 10, 24);

    public static DateOnly Departure { get; } = new(2026, 10, 27);

    public static Guest Guest(int idSuffix = 1)
    {
        Guest guest = global::HospitalityOS.Domain.Guests.Guests.Guest.Register(new GuestId(Id(idSuffix)), TenantId, new GuestProfile("Alex", "en"), Now).Value;
        guest.DequeueDomainEvents();

        return guest;
    }

    public static StayContext Stay(Guest? guest = null, PropertyId? propertyId = null, AccommodationUnitId? unit = null, int idSuffix = 10)
    {
        StayContext stay = StayContext
            .Create(new StayContextId(Id(idSuffix)), guest ?? Guest(), propertyId ?? PropertyId, Arrival, Departure, unit, Now)
            .Value;
        stay.DequeueDomainEvents();

        return stay;
    }

    public static Conversation Conversation(int idSuffix = 20)
    {
        Conversation conversation = global::HospitalityOS.Domain.Guests.Conversations.Conversation
            .Start(new ConversationId(Id(idSuffix)), TenantId, PropertyId, ConversationChannel.Web, Now)
            .Value;
        conversation.DequeueDomainEvents();

        return conversation;
    }

    public static Guid Id(int suffix) => Guid.Parse($"01890a5d-ac96-774b-bcce-b302099{suffix:D5}");
}
