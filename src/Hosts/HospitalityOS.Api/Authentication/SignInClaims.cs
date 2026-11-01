using System.Security.Claims;
using HospitalityOS.Domain.Organizations.Access;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Staff sign-in through the identity provider (K4b, Microsoft Entra External ID): its tokens prove who someone is, and
/// <see cref="IStaffAccessDirectory"/> decides who they act as. A validated token's principal is replaced by one
/// carrying the sign-in (<c>signin_iss</c>, <c>signin_sub</c> from the token's <c>iss</c> and <c>oid</c>), the
/// <c>Operator</c> role when the provider gives it, and, when the sign-in is linked to active access, the same staff
/// claims a staff token carries (<see cref="StaffClaims"/>) plus <c>access_id</c>. So every staff policy, the caller
/// context, and the audit trail work unchanged, and revoked access is refused on the next request.
/// </summary>
internal static class SignInClaims
{
    public const string Scheme = "StaffSignIn";
    public const string Issuer = "signin_iss";
    public const string Subject = "signin_sub";
    public const string Access = "access_id";
    public const string OperatorRole = "Operator";

    // Entra's stable user ID within the directory; sub is per application.
    private const string ObjectId = "oid";
    private const string ProviderRoles = "roles";

    /// <summary>
    /// Whether the caller signed in through the identity provider.
    /// </summary>
    public static bool IsSignIn(ClaimsPrincipal principal) => principal.HasClaim(claim => claim.Type == Subject);

    /// <summary>
    /// The caller's sign-in, when they signed in through the identity provider.
    /// </summary>
    public static bool TryRead(ClaimsPrincipal principal, out string issuer, out string subject)
    {
        issuer = principal.FindFirst(Issuer)?.Value ?? string.Empty;
        subject = principal.FindFirst(Subject)?.Value ?? string.Empty;
        return issuer.Length > 0 && subject.Length > 0;
    }

    /// <summary>
    /// The access a linked sign-in acts as.
    /// </summary>
    public static Guid? AccessId(ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirst(Access)?.Value, out Guid id) ? id : null;

    /// <summary>
    /// Builds the caller from a validated provider token; fails it when it names nobody.
    /// </summary>
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        string? issuer = (context.SecurityToken as JsonWebToken)?.Issuer;
        string? subject = context.Principal?.FindFirst(ObjectId)?.Value;

        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(subject))
        {
            context.Fail($"A sign-in token needs an issuer and an '{ObjectId}' claim.");
            return;
        }

        List<Claim> claims = [new(Issuer, issuer), new(Subject, subject)];

        if (context.Principal!.FindAll(ProviderRoles).Any(role => role.Value == OperatorRole))
        {
            claims.Add(new Claim(StaffClaims.Role, OperatorRole));
        }

        IStaffAccessDirectory directory = context.HttpContext.RequestServices.GetRequiredService<IStaffAccessDirectory>();

        if (await directory.FindBySignInAsync(issuer, subject, context.HttpContext.RequestAborted).ConfigureAwait(false) is { } signIn)
        {
            claims.Add(new Claim(StaffClaims.Subject, signIn.ActorId));
            claims.Add(new Claim(StaffClaims.Tenant, signIn.TenantId.Value.ToString()));
            claims.Add(new Claim(StaffClaims.Role, signIn.Role.ToString()));
            claims.Add(new Claim(Access, signIn.AccessId.Value.ToString()));
        }

        context.Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme, StaffClaims.Subject, StaffClaims.Role));
    }

    /// <summary>
    /// Whether a bearer token's issuer, read without validating it (the chosen scheme validates it), is the identity
    /// provider's rather than a staff or stay token issuer's.
    /// </summary>
    public static bool IsProviderIssuer(string? issuer, JwtBearerOptions signIn)
    {
        if (string.IsNullOrEmpty(issuer))
        {
            return false;
        }

        IEnumerable<string> issuers = [.. signIn.TokenValidationParameters.ValidIssuers ?? [], .. signIn.TokenValidationParameters.ValidIssuer is { } one ? [one] : Array.Empty<string>()];
        return issuers.Contains(issuer, StringComparer.Ordinal);
    }
}
