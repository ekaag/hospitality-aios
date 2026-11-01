using Anthropic;
using HospitalityOS.Adapters.Anthropic;
using HospitalityOS.Ports.Secrets;
using Xunit;

namespace HospitalityOS.UnitTests.Adapters.Anthropic;

/// <summary>
/// With <c>ApiKeySecret</c>, the client is made once with the key from the secret store; a missing secret fails the
/// call, and the next call reads the store again.
/// </summary>
public sealed class AnthropicClientSourceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FromSecret_MakesTheClientOnce_WithTheSecretsKey()
    {
        Secrets secrets = new() { Value = "sk-ant-from-vault" };
        List<string?> keys = [];
        AnthropicClientSource source = AnthropicClientSource.FromSecret(secrets, "anthropic-api-key", key =>
        {
            keys.Add(key);

            return new AnthropicClient();
        });

        IAnthropicClient first = await source.GetAsync(Cancellation);
        IAnthropicClient second = await source.GetAsync(Cancellation);

        Assert.Same(first, second);
        Assert.Equal(["sk-ant-from-vault"], keys);
        Assert.Equal(["anthropic-api-key"], secrets.Asked);
    }

    [Fact]
    public async Task FromSecret_SecretMissing_Fails_ThenTriesAgain()
    {
        Secrets secrets = new();
        AnthropicClientSource source = AnthropicClientSource.FromSecret(secrets, "anthropic-api-key", _ => new AnthropicClient());

        InvalidOperationException missing = await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetAsync(Cancellation).AsTask());
        secrets.Value = "sk-ant-set-later";
        IAnthropicClient client = await source.GetAsync(Cancellation);

        Assert.Contains("anthropic-api-key", missing.Message, StringComparison.Ordinal);
        Assert.NotNull(client);
        Assert.Equal(2, secrets.Asked.Count);
    }

    private sealed class Secrets : ISecretStore
    {
        public string? Value { get; set; }

        public List<string> Asked { get; } = [];

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken)
        {
            Asked.Add(name);

            return Task.FromResult(Value);
        }
    }
}
