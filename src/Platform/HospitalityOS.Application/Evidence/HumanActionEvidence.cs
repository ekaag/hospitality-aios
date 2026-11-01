using System.Text.RegularExpressions;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Ids;
using HospitalityOS.Ports.Storage;
using HospitalityOS.Ports.Tenancy;
using HospitalityOS.SharedKernel.Results;

namespace HospitalityOS.Application.Evidence;

/// <summary>
/// Photos and files staff attach to their work (Journey 14), kept in the bound <see cref="IBlobStore"/> under the current
/// tenant (P10). An evidence key, the one completion records, is <c>{work}/{file}</c> (two 32-digit IDs): it names the
/// work it belongs to, so a key cannot be moved onto other work, and it is looked up under the caller's own tenant, so
/// another tenant's key names nothing. A file is kept once uploaded, even if the work is never completed.
/// </summary>
public sealed partial class HumanActionEvidence(
    IBindingResolver<IBlobStore> blobStores,
    IBindingResolver<IIdGenerator> idGenerators,
    ICurrentTenant currentTenant)
{
    /// <summary>
    /// The largest file, 10 MiB: a phone photo or a short PDF.
    /// </summary>
    public const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The media types accepted: photos as phones take them, and PDFs.
    /// </summary>
    public static IReadOnlySet<string> ContentTypes { get; } =
        new HashSet<string>(["image/jpeg", "image/png", "image/webp", "image/heic", "application/pdf"], StringComparer.Ordinal);

    /// <summary>
    /// Saves a file for <paramref name="humanActionId"/>.
    /// </summary>
    /// <returns>Its evidence key, or why it was refused.</returns>
    public async Task<Result<string>> AddAsync(Guid humanActionId, Stream content, string? contentType, long length, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        List<FieldError> errors = [];
        string type = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();

        if (!ContentTypes.Contains(type))
        {
            errors.Add(new FieldError("file", "evidence.unsupported_type", $"Evidence must be a photo (JPEG, PNG, WebP, HEIC) or a PDF, not '{contentType}'."));
        }

        if (length <= 0)
        {
            errors.Add(new FieldError("file", "evidence.empty", "The file is empty."));
        }
        else if (length > MaxBytes)
        {
            errors.Add(new FieldError("file", "evidence.too_large", $"Evidence files may be at most {MaxBytes / (1024 * 1024)} MiB."));
        }

        if (errors.Count > 0)
        {
            return ValidationError.From(errors);
        }

        string key = $"{humanActionId:N}/{idGenerators.Resolve().NewId():N}";

        // A new ID is never taken, so the write cannot find the key in use.
        await blobStores.Resolve().PutAsync(BlobKey(key), content, type, cancellationToken).ConfigureAwait(false);

        return key;
    }

    /// <summary>
    /// Opens a file of <paramref name="humanActionId"/>'s; the caller disposes it.
    /// </summary>
    /// <returns>The file, or null when the key is not one of this work's files.</returns>
    public async Task<StoredBlob?> OpenAsync(Guid humanActionId, string key, CancellationToken cancellationToken) =>
        Belongs(humanActionId, key) ? await blobStores.Resolve().OpenAsync(BlobKey(key), cancellationToken).ConfigureAwait(false) : null;

    /// <summary>
    /// The keys that are not files uploaded for <paramref name="humanActionId"/> in this tenant, in the order given.
    /// </summary>
    public async Task<IReadOnlyList<string>> MissingAsync(Guid humanActionId, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        List<string> missing = [];
        IBlobStore store = blobStores.Resolve();

        foreach (string key in keys)
        {
            if (!Belongs(humanActionId, key) || !await store.ExistsAsync(BlobKey(key), cancellationToken).ConfigureAwait(false))
            {
                missing.Add(key);
            }
        }

        return missing;
    }

    private static bool Belongs(Guid humanActionId, string? key) =>
        key is not null && Key().Match(key) is { Success: true } match && match.Groups["work"].Value == humanActionId.ToString("N");

    private string BlobKey(string key) => $"tenants/{currentTenant.TenantId.Value:N}/evidence/{key}";

    [GeneratedRegex("^(?<work>[0-9a-f]{32})/[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Key();
}
