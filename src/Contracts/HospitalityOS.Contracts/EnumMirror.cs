namespace HospitalityOS.Contracts;

/// <summary>
/// Converts between a domain enum and its contract mirror by member name. The contract enums are separate types so
/// the published schemas never change with the domain; a test keeps their member names identical.
/// </summary>
public static class EnumMirror
{
    /// <summary>
    /// The member of <typeparamref name="TTarget"/> with the same name as <paramref name="value"/>.
    /// </summary>
    /// <exception cref="ArgumentException">No member has that name: the mirror test should have caught it.</exception>
    public static TTarget To<TTarget>(Enum value)
        where TTarget : struct, Enum =>
        Enum.Parse<TTarget>(value.ToString());
}
