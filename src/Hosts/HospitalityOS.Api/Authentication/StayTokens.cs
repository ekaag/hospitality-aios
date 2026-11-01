using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HospitalityOS.Application.Conversations;
using HospitalityOS.Application.GuestLinks;
using HospitalityOS.Contracts.Api.GuestSessions;
using HospitalityOS.Domain.Guests.GuestLinks;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Security;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// Guests' stay tokens issued by the API itself (K4c), for the sessions check-in links open: the claims of
/// <see cref="GuestClaims"/> plus <c>link_id</c>, signed RS256 by the bound <see cref="ISigningKey"/> (a Key Vault key in
/// the cloud, whose private half never leaves the vault) and checked by the <see cref="Scheme"/> scheme with its public
/// half. Every request also checks the link is still open and its stay still reserved or in house, so revoking a link or
/// checking out ends its sessions at once; a token's short life only bounds what a stolen one is worth.
/// </summary>
internal static class StayTokens
{
    public const string Scheme = "StayToken";
    public const string Issuer = "https://stay-tokens.hospitalityos";
    public const string Audience = "hospitalityos-api";
    public const string Link = "link_id";

    /// <summary>
    /// How long a stay token works; the guest app opens a session again with the same link after.
    /// </summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromHours(8);

    /// <summary>
    /// A signed stay token for the session.
    /// </summary>
    public static async Task<StayTokenResponse> IssueAsync(GuestSession session, ISigningKey key, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(key);
        SigningPublicKey publicKey = await key.GetPublicKeyAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expires = now + Lifetime;
        string header = Encode(new Dictionary<string, object> { ["alg"] = SecurityAlgorithms.RsaSha256, ["typ"] = "JWT", ["kid"] = publicKey.KeyId });
        string payload = Encode(new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expires.ToUnixTimeSeconds(),
            [StaffClaims.Subject] = session.Caller.GuestId.Value.ToString(),
            [StaffClaims.Tenant] = session.TenantId.Value.ToString(),
            [StaffClaims.Role] = GuestClaims.Role,
            [GuestClaims.Property] = session.Caller.PropertyId.Value.ToString(),
            [GuestClaims.Stay] = session.Caller.StayContextId.Value.ToString(),
            [Link] = session.LinkId.Value.ToString(),
        });
        string signed = $"{header}.{payload}";
        Signature signature = await key.SignAsync(Encoding.ASCII.GetBytes(signed), cancellationToken);

        return new StayTokenResponse($"{signed}.{Base64UrlEncoder.Encode(signature.Value)}", expires);
    }

    /// <summary>
    /// Whether a bearer token claims to be a stay token, read without validating it (the scheme validates it).
    /// </summary>
    public static bool IsStayToken(string? issuer) => issuer == Issuer;

    /// <summary>
    /// The stay token scheme's checks, after configuration binding.
    /// </summary>
    public static void Configure(JwtBearerOptions options, Func<SigningPublicKey?> currentKey)
    {
        options.TokenValidationParameters.ValidIssuer = Issuer;
        options.TokenValidationParameters.ValidAudience = Audience;
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, keyId, _) =>
            currentKey() is { } key && key.KeyId == keyId
                ? [new RsaSecurityKey(new System.Security.Cryptography.RSAParameters { Modulus = key.Modulus, Exponent = key.Exponent }) { KeyId = key.KeyId }]
                : [];

        JwtBearerEvents events = options.Events;
        Func<MessageReceivedContext, Task> received = events.OnMessageReceived;
        events.OnMessageReceived = async context =>
        {
            await received(context);

            // Warms the key cache, so the synchronous resolver above has it.
            await context.HttpContext.RequestServices.GetRequiredService<StayTokenKeys>().RefreshAsync(context.HttpContext.RequestAborted);
        };
        events.OnTokenValidated = async context =>
        {
            ClaimsPrincipal principal = context.Principal!;

            if (!GuestClaims.IsGuest(principal) || !GuestClaims.TryRead(principal, out TenantId tenant, out GuestCaller? caller, out string? problem))
            {
                context.Fail("A stay token must carry a guest's claims.");
                return;
            }

            if (!Guid.TryParse(principal.FindFirst(Link)?.Value, out Guid link) || link == Guid.Empty)
            {
                context.Fail($"A stay token has no valid '{Link}' claim.");
                return;
            }

            IServiceProvider services = context.HttpContext.RequestServices;
            services.GetRequiredService<TenantContext>().Set(tenant);

            if (!await services.GetRequiredService<IGuestSessions>().IsOpenAsync(new GuestLinkId(link), caller!, context.HttpContext.RequestAborted))
            {
                context.Fail("The stay token's link is revoked or its stay has ended.");
            }
        };
    }

    private static string Encode(Dictionary<string, object> values) => Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(values));
}

/// <summary>
/// The signing key's public half for checking stay tokens, read from the bound key at most once a minute.
/// </summary>
internal sealed class StayTokenKeys(IBindingResolver<ISigningKey> keys, TimeProvider timeProvider)
{
    private static readonly TimeSpan refreshEvery = TimeSpan.FromMinutes(1);
    private SigningPublicKey? current;
    private DateTimeOffset refreshedAt = DateTimeOffset.MinValue;

    public SigningPublicKey? Current => Volatile.Read(ref current);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (current is not null && timeProvider.GetUtcNow() - refreshedAt < refreshEvery)
        {
            return;
        }

        Volatile.Write(ref current, await keys.Resolve().GetPublicKeyAsync(cancellationToken));
        refreshedAt = timeProvider.GetUtcNow();
    }
}
