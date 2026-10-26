using HospitalityOS.Ports.Eventing;

namespace HospitalityOS.Adapters.EfCore.Eventing;

/// <summary>
/// One handler's committed processing of one event; the key (handler, event) is unique.
/// </summary>
internal sealed class InboxRecord
{
    public const int MaxHandlerLength = IInboxStore.MaxHandlerLength;

    public string Handler { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public DateTimeOffset ProcessedAt { get; set; }
}
