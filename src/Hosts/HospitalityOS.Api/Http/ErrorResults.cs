using HospitalityOS.Application.Administration;
using HospitalityOS.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HospitalityOS.Api.Http;

/// <summary>
/// Expected failures as RFC 9457 problem details, one HTTP status per <see cref="ErrorKind"/>, with the stable error
/// <c>code</c> (and each invalid field's code) for clients to act on.
/// </summary>
internal static class ErrorResults
{
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error is ValidationError validation)
        {
            return TypedResults.ValidationProblem(
                validation.Fields.GroupBy(field => field.Field).ToDictionary(group => group.Key, group => group.Select(field => field.Message).ToArray()),
                title: "The request has invalid fields.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = error.Code,
                    ["fieldErrors"] = validation.Fields.Select(field => new { field.Field, field.Code, field.Message }).ToList(),
                });
        }

        int status = error.Code == AdministrationErrors.VersionMismatchCode ? StatusCodes.Status412PreconditionFailed : error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.InvalidState => StatusCodes.Status422UnprocessableEntity,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest,
        };

        return TypedResults.Problem(new ProblemDetails { Status = status, Title = error.Message, Extensions = { ["code"] = error.Code } });
    }
}
