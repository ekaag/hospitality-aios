using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Results;

public sealed class ValidationErrorTests
{
    private static readonly FieldError nameRequired = new("name", "property.name_required", "Name is required.");
    private static readonly FieldError timeZoneInvalid = new("timeZone", "property.time_zone_invalid", "Time zone is invalid.");

    [Fact]
    public void From_WithSeveralFields_KeepsAllInOrder()
    {
        ValidationError error = ValidationError.From([nameRequired, timeZoneInvalid]);

        Assert.Equal(ErrorKind.Validation, error.Kind);
        Assert.Equal(ValidationError.ValidationFailedCode, error.Code);
        Assert.Equal([nameRequired, timeZoneInvalid], error.Fields);
        Assert.Equal("2 fields are invalid.", error.Message);
    }

    [Fact]
    public void From_WithOneField_UsesFieldMessage()
    {
        ValidationError error = ValidationError.From([nameRequired]);

        Assert.Equal("Name is required.", error.Message);
    }

    [Fact]
    public void From_WithNoFields_Throws()
    {
        Assert.Throws<ArgumentException>(() => ValidationError.From([]));
    }

    [Fact]
    public void From_CopiesFields_SoLaterChangesToSourceAreIgnored()
    {
        List<FieldError> source = [nameRequired];

        ValidationError error = ValidationError.From(source);
        source.Add(timeZoneInvalid);

        Assert.Single(error.Fields);
    }

    [Fact]
    public void ErrorValidation_CreatesSingleFieldError()
    {
        ValidationError error = Error.Validation("name", "property.name_required", "Name is required.");

        Assert.Equal([nameRequired], error.Fields);
    }

    [Fact]
    public void ValidationError_ConvertsToFailedResult()
    {
        Result result = Error.Validation("name", "property.name_required", "Name is required.");

        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        Assert.Single(error.Fields);
    }
}
