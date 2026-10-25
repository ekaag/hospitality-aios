using HospitalityOS.SharedKernel.Results;
using Xunit;

namespace HospitalityOS.UnitTests.SharedKernel.Results;

public sealed class ResultTests
{
    private static readonly Error notFound = Error.NotFound("property.not_found", "Property was not found.");

    [Fact]
    public void Success_IsSuccessWithNoError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_IsFailureCarryingError()
    {
        Result result = Result.Failure(notFound);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Same(notFound, result.Error);
    }

    [Fact]
    public void Failure_WithNullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void ImplicitConversion_FromError_IsFailure()
    {
        Result result = notFound;

        Assert.Same(notFound, result.Error);
    }

    [Fact]
    public void SuccessOfT_CarriesValue()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void FailureOfT_ReadingValue_Throws()
    {
        Result<int> result = Result.Failure<int>(notFound);

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversionOfT_FromValueAndError_SetsOutcome()
    {
        Result<string> success = "ok";
        Result<string> failure = notFound;

        Assert.Equal("ok", success.Value);
        Assert.Same(notFound, failure.Error);
    }

    [Theory]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.Conflict)]
    [InlineData(ErrorKind.InvalidState)]
    public void ErrorFactories_SetKind(ErrorKind kind)
    {
        Error error = kind switch
        {
            ErrorKind.NotFound => Error.NotFound("code", "message"),
            ErrorKind.Conflict => Error.Conflict("code", "message"),
            _ => Error.InvalidState("code", "message"),
        };

        Assert.Equal(new Error("code", "message", kind), error);
    }
}
