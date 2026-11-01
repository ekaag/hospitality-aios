using System.Security.Cryptography;
using HospitalityOS.Ports.Bindings;
using HospitalityOS.Ports.Security;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Adapters.Local.Security;

/// <summary>
/// <see cref="ISigningKey"/> on an RSA key made when the process starts and never stored, so nothing is configured or
/// leaked and every token it signed stops working when the process restarts (as the dev tokens do).
/// </summary>
internal sealed class ProcessSigningKey : ISigningKey, IDisposable
{
    private readonly RSA rsa = RSA.Create(2048);
    private readonly SigningPublicKey publicKey;

    public ProcessSigningKey()
    {
        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: false);
        publicKey = new SigningPublicKey($"process-{Guid.NewGuid():N}", parameters.Modulus!, parameters.Exponent!);
    }

    public Task<SigningPublicKey> GetPublicKeyAsync(CancellationToken cancellationToken) => Task.FromResult(publicKey);

    public Task<Signature> SignAsync(byte[] data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Signature(publicKey.KeyId, rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)));
    }

    public void Dispose() => rsa.Dispose();
}

/// <summary>
/// Binds <see cref="ISigningKey"/> to <see cref="ProcessSigningKey"/> under the adapter name <c>Process</c>; no settings.
/// </summary>
internal sealed class ProcessSigningKeyFactory : IAdapterFactory<ISigningKey>
{
    public const string Name = "Process";

    public string AdapterName => Name;

    public ISigningKey Create(IServiceProvider services, IConfiguration settings) => new ProcessSigningKey();

    public IEnumerable<string> ValidateSettings(IConfiguration settings) => [];
}
