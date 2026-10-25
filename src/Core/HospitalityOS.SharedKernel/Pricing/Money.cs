namespace HospitalityOS.SharedKernel.Pricing;

/// <summary>
/// An amount in one currency. The AI quotes prices only from these canonical values (spec Journey 7).
/// </summary>
/// <param name="Amount">The amount, non-negative, in major units (e.g. 12.50).</param>
/// <param name="CurrencyCode">ISO 4217 code, upper case, e.g. <c>USD</c>.</param>
public sealed record Money(decimal Amount, string CurrencyCode)
{
    /// <inheritdoc />
    public override string ToString() => $"{Amount:0.00} {CurrencyCode}";
}
