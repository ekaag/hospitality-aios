namespace HospitalityOS.SharedKernel.Ids;

/// <summary>
/// Guards shared by strongly typed ID constructors.
/// </summary>
public static class TypedIds
{
    /// <summary>
    /// Returns <paramref name="value"/>, or throws when it is <see cref="Guid.Empty"/>: an empty ID is a programming error.
    /// </summary>
    /// <param name="value">The candidate identifier.</param>
    /// <param name="idTypeName">The ID type, for the exception message.</param>
    /// <returns>The non-empty identifier.</returns>
    public static Guid EnsureNotEmpty(Guid value, string idTypeName)
    {
        return value == Guid.Empty
            ? throw new ArgumentException($"{idTypeName} cannot be empty.", nameof(value))
            : value;
    }
}
