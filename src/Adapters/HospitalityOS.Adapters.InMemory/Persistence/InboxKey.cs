namespace HospitalityOS.Adapters.InMemory.Persistence;

/// <summary>
/// One handler's processing of one event: unique, like the database's inbox key.
/// </summary>
internal readonly record struct InboxKey(string Handler, Guid EventId);
