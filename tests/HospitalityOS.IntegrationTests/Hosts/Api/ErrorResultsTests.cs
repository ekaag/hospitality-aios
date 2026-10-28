using HospitalityOS.Api.Http;
using HospitalityOS.Application.Administration;
using HospitalityOS.SharedKernel.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Each kind of expected failure has one HTTP status, so clients can act on it without parsing messages.
/// </summary>
public sealed class ErrorResultsTests
{
    [Theory]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.InvalidState, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    public void ToProblem_GivesEachKindItsStatus_AndTheCode(ErrorKind kind, int status)
    {
        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(new Error("area.reason", "What happened.", kind).ToProblem());

        Assert.Equal(status, problem.StatusCode);
        Assert.Equal("area.reason", problem.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void ToProblem_AStaleVersion_IsPreconditionFailed()
    {
        ProblemHttpResult problem = Assert.IsType<ProblemHttpResult>(AdministrationErrors.VersionMismatch(1, 2).ToProblem());

        Assert.Equal(StatusCodes.Status412PreconditionFailed, problem.StatusCode);
    }
}
