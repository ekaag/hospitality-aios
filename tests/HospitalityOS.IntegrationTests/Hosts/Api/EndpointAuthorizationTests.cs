using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HospitalityOS.IntegrationTests.Hosts.Api;

/// <summary>
/// Role-based authorization on every API (P10, spec §42 Security): every endpoint the API maps names a policy or roles,
/// except the few meant for anyone, which must say so and are listed here, so a new endpoint without a policy fails the
/// build rather than going out open.
/// </summary>
public sealed class EndpointAuthorizationTests
{
    /// <summary>
    /// The endpoints anyone may call, and why.
    /// </summary>
    private static readonly string[] open =
    [
        "/health", // Liveness and readiness probes.
        "/openapi/{documentName}.json", // The frontends' contract, in Development only.
        "/api/dev/tokens", // Dev sign-in, in Development only.
        "/api/dev/tokens/personas",
        "/api/v1/guest-sessions", // A check-in link's code is the credential.
    ];

    [Fact]
    public void EveryEndpoint_RequiresAPolicyOrRoles_ExceptThoseMeantForAnyone()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:DevTokens:Enabled", "true"));
        factory.CreateClient().Dispose();
        IEnumerable<RouteEndpoint> endpoints = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>();
        List<string> unprotected = [];
        List<string> anonymous = [];

        foreach (RouteEndpoint endpoint in endpoints)
        {
            string route = "/" + endpoint.RoutePattern.RawText?.Trim('/');
            string name = $"{string.Join(",", endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])} {route}";
            bool allowsAnyone = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            bool authorized = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy is not null || data.Roles is not null || data.AuthenticationSchemes is not null)
                || endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Any();

            if (allowsAnyone)
            {
                anonymous.Add(route);
            }

            if (!open.Contains(route, StringComparer.Ordinal) && (allowsAnyone || !authorized))
            {
                unprotected.Add(name);
            }
        }

        Assert.Empty(unprotected);
        Assert.Subset(open.ToHashSet(StringComparer.Ordinal), anonymous.Where(route => open.Contains(route, StringComparer.Ordinal)).ToHashSet(StringComparer.Ordinal));
    }
}
