using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Api.Http;

/// <summary>
/// A result for one versioned record: adds its <c>ETag</c>, so the caller can send it back in <c>If-Match</c>.
/// </summary>
internal sealed class VersionedResult(IResult inner, long version) : IResult
{
    /// <summary>
    /// The record with its <c>ETag</c> (200), or the failure as a problem.
    /// </summary>
    public static IResult Ok<T>(Result<T> result, Func<T, long> version)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(version);

        return result.IsFailure ? result.Error.ToProblem() : new VersionedResult(TypedResults.Ok(result.Value), version(result.Value));
    }

    /// <summary>
    /// The new record with its <c>Location</c> and <c>ETag</c> (201), or the failure as a problem.
    /// </summary>
    public static IResult Created<T>(Result<T> result, Func<T, string> location, Func<T, long> version)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(version);

        return result.IsFailure ? result.Error.ToProblem() : new VersionedResult(TypedResults.Created(location(result.Value), result.Value), version(result.Value));
    }

    /// <summary>
    /// Runs a change conditional on the request's <c>If-Match</c> (a malformed header is refused before the change runs)
    /// and returns the changed record with its <c>ETag</c>.
    /// </summary>
    public static async Task<IResult> ChangeAsync<T>(HttpRequest request, Func<long?, Task<Result<T>>> change, Func<T, long> version)
    {
        ArgumentNullException.ThrowIfNull(change);

        Result<long?> expected = Versions.ExpectedVersion(request);

        return expected.IsFailure ? expected.Error.ToProblem() : Ok(await change(expected.Value).ConfigureAwait(false), version);
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Headers.ETag = Versions.ETag(version);

        return inner.ExecuteAsync(httpContext);
    }
}
