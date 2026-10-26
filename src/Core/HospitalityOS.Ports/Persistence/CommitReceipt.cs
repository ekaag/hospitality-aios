namespace HospitalityOS.Ports.Persistence;

/// <summary>
/// What a successful <see cref="IUnitOfWork.CommitAsync"/> saved.
/// </summary>
/// <param name="Events">The events saved with the aggregates: each aggregate's events in the order raised, aggregates in
/// the order the scope first added or loaded them; empty when nothing changed.</param>
public sealed record CommitReceipt(IReadOnlyList<CommittedEvent> Events)
{
    /// <summary>
    /// A commit that saved no events.
    /// </summary>
    public static CommitReceipt Empty { get; } = new([]);
}
