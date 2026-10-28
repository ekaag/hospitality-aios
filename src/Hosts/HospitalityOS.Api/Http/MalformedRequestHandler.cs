using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace HospitalityOS.Api.Http;

/// <summary>
/// A request the API cannot read (malformed JSON, a value of the wrong type, an unknown name for a choice, a missing
/// or invalid route or query value) as a problem with code <c>request.malformed</c> saying where, instead of an empty
/// response. Requests that can be read but are invalid are reported field by field by validation instead.
/// </summary>
internal sealed class MalformedRequestHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public const string Code = "request.malformed";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        // The JSON path names the field without the .NET type names the exception message carries.
        string detail = badRequest.InnerException is JsonException { Path: { } path }
            ? $"The value at {path} could not be read."
            : badRequest.Message;
        httpContext.Response.StatusCode = badRequest.StatusCode;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = badRequest.StatusCode,
                Title = "The request could not be read.",
                Detail = detail,
                Extensions = { ["code"] = Code },
            },
        }).ConfigureAwait(false);
    }
}
