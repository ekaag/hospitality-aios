namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// A strongly typed identifier over a <see cref="Guid"/>. Each aggregate defines its own
/// <c>readonly record struct &lt;Aggregate&gt;Id</c> so IDs of different kinds cannot be mixed up.
/// </summary>
public interface ITypedId
{
    /// <summary>
    /// The underlying identifier (GUID v7, generated through <c>IIdGenerator</c>).
    /// </summary>
    Guid Value { get; }
}
