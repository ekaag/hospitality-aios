namespace HospitalityOS.Capabilities.Reading;

/// <summary>
/// Converts between a domain enum and its contract mirror by member name. The contract enums are separate types so
/// the published schemas never change with the domain; a test keeps their member names identical.
/// </summary>
internal static class ContractEnum
{
    public static TTarget To<TTarget>(Enum value)
        where TTarget : struct, Enum =>
        Enum.Parse<TTarget>(value.ToString());
}
