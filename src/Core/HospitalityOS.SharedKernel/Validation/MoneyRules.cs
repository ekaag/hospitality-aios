using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.SharedKernel.Validation;

/// <summary>
/// Validates and normalizes <see cref="Money"/> values, recording field errors instead of failing.
/// </summary>
public static class MoneyRules
{
    /// <summary>
    /// Largest accepted amount; guards against typos, not real prices.
    /// </summary>
    public const decimal MaxAmount = 1_000_000m;

    /// <summary>
    /// Normalizes the currency code to upper case; records <c>{codePrefix}_amount_invalid</c> (negative, too
    /// large, or more than two decimal places) or <c>{codePrefix}_currency_invalid</c> under <c>{field}.amount</c>
    /// and <c>{field}.currencyCode</c>.
    /// </summary>
    public static Money Validate(Money money, string field, string codePrefix, List<FieldError> errors)
    {
        ArgumentNullException.ThrowIfNull(money);

        string currency = money.CurrencyCode?.Trim().ToUpperInvariant() ?? "";

        if (money.Amount < 0 || money.Amount > MaxAmount || decimal.Round(money.Amount, 2) != money.Amount)
        {
            errors.Add(new FieldError(field + ".amount", codePrefix + "_amount_invalid", $"Amount must be between 0 and {MaxAmount:N0} with at most two decimal places."));
        }

        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            errors.Add(new FieldError(field + ".currencyCode", codePrefix + "_currency_invalid", "Currency must be a three-letter ISO 4217 code, e.g. 'USD'."));
        }

        return money with { CurrencyCode = currency };
    }
}
