using HospitalityOS.SharedKernel.Pricing;
using HospitalityOS.SharedKernel.Results;
using HospitalityOS.SharedKernel.Validation;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Validation;

public sealed class MoneyRulesTests
{
    [Fact]
    public void Validate_WithValidMoney_UpperCasesCurrency()
    {
        List<FieldError> errors = [];

        Money money = MoneyRules.Validate(new Money(12.5m, " usd "), "price", "item_price", errors);

        Assert.Empty(errors);
        Assert.Equal(new Money(12.5m, "USD"), money);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1000000.01")]
    [InlineData("1.001")]
    public void Validate_WithBadAmount_RecordsAmountError(string amount)
    {
        List<FieldError> errors = [];

        MoneyRules.Validate(new Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), "USD"), "price", "item_price", errors);

        Assert.Equal([new FieldError("price.amount", "item_price_amount_invalid", errors[0].Message)], errors);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("US1")]
    [InlineData("")]
    public void Validate_WithBadCurrency_RecordsCurrencyError(string currency)
    {
        List<FieldError> errors = [];

        MoneyRules.Validate(new Money(1m, currency), "price", "item_price", errors);

        Assert.Equal("price.currencyCode", Assert.Single(errors).Field);
    }
}
