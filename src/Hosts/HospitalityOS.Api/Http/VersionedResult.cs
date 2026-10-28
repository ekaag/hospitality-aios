namespace HospitalityOS.Api.Http;

/// <summary>
/// A result for one versioned record: adds its <c>ETag</c>, so the caller can send it back in <c>If-Match</c>.
/// </summary>
internal sealed class VersionedResult(IResult inner, long version) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.Headers.ETag = Versions.ETag(version);

        return inner.ExecuteAsync(httpContext);
    }
}
