namespace HospitalityOS.Bindings;

/// <summary>
/// Marker options type whose validation runs once at host startup (via <c>ValidateOnStart</c>)
/// and checks every registered port's bindings.
/// </summary>
internal sealed class BindingStartupCheck
{
}
