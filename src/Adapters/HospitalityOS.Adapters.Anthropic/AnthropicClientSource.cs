using Anthropic;
using HospitalityOS.Ports.Secrets;

namespace HospitalityOS.Adapters.Anthropic;

/// <summary>
/// The client a provider calls Claude with. When the provider names an <c>ApiKeySecret</c>, the client is made on first
/// use with the key read from <see cref="ISecretStore"/>, and kept; a failed read is not kept, so the next call tries
/// again. A rotated key is picked up on restart.
/// </summary>
internal sealed class AnthropicClientSource
{
    private readonly Lock gate = new();
    private readonly Func<string?, IAnthropicClient> create;
    private readonly ISecretStore? secrets;
    private readonly string? secretName;
    private IAnthropicClient? client;
    private Task<IAnthropicClient>? making;

    private AnthropicClientSource(Func<string?, IAnthropicClient> create, ISecretStore? secrets, string? secretName, IAnthropicClient? client)
    {
        this.create = create;
        this.secrets = secrets;
        this.secretName = secretName;
        this.client = client;
    }

    /// <summary>
    /// A client that is ready now: its key came from configuration's variable or the SDK's own lookup.
    /// </summary>
    public static AnthropicClientSource Of(IAnthropicClient client) => new(_ => client, null, null, client);

    /// <summary>
    /// A client made with the key in secret <paramref name="secretName"/>.
    /// </summary>
    public static AnthropicClientSource FromSecret(ISecretStore secrets, string secretName, Func<string?, IAnthropicClient> create) =>
        new(create, secrets, secretName, null);

    public async ValueTask<IAnthropicClient> GetAsync(CancellationToken cancellationToken)
    {
        if (client is { } ready)
        {
            return ready;
        }

        Task<IAnthropicClient> pending;

        lock (gate)
        {
            pending = making ??= MakeAsync();
        }

        try
        {
            return client = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (pending.IsFaulted)
        {
            lock (gate)
            {
                if (making == pending)
                {
                    making = null;
                }
            }

            throw;
        }
    }

    private async Task<IAnthropicClient> MakeAsync()
    {
        string key = await secrets!.GetAsync(secretName!, CancellationToken.None).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The secret '{secretName}' holding the Anthropic API key is not set.");

        return create(key);
    }
}
