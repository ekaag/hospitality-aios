namespace HospitalityOS.Ports.Ids;

/// <summary>
/// Generates canonical identifiers. Domain and application code wrap the result in a strongly typed ID
/// (e.g. <c>new PropertyId(idGenerator.NewId())</c>) and never call <see cref="Guid.NewGuid"/> directly,
/// so tests can supply deterministic IDs (P13).
/// </summary>
public interface IIdGenerator
{
    /// <summary>
    /// Returns a new, unique, non-empty identifier.
    /// </summary>
    /// <returns>The identifier.</returns>
    Guid NewId();
}
