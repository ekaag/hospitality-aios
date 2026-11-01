using System.Security.Cryptography;
using System.Text;
using HospitalityOS.Ports.Security;
using Xunit;

namespace HospitalityOS.PortContractTests.Security;

/// <summary>
/// What every <see cref="ISigningKey"/> adapter must do: sign with RS256 so that the public key it gives verifies the
/// signature, name the key that signed, and never verify a signature over other data.
/// </summary>
public abstract class SigningKeyContractTests
{
    private readonly Lazy<ISigningKey> key;

    protected SigningKeyContractTests()
    {
        key = new Lazy<ISigningKey>(CreateKey);
    }

    protected static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sign_IsVerifiedByThePublicKey_AndNamesIt()
    {
        byte[] data = Encoding.ASCII.GetBytes("eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJhbGV4In0");

        Signature signature = await key.Value.SignAsync(data, Cancellation);
        SigningPublicKey publicKey = await key.Value.GetPublicKeyAsync(Cancellation);

        Assert.Equal(publicKey.KeyId, signature.KeyId);
        Assert.True(Verify(publicKey, data, signature.Value));
        Assert.False(Verify(publicKey, Encoding.ASCII.GetBytes("eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJtYWxsb3J5In0"), signature.Value));
    }

    [Fact]
    public async Task ThePublicKey_IsTheSameForEveryCall_UntilRotated()
    {
        SigningPublicKey first = await key.Value.GetPublicKeyAsync(Cancellation);
        SigningPublicKey second = await key.Value.GetPublicKeyAsync(Cancellation);

        Assert.Equal(first.KeyId, second.KeyId);
        Assert.Equal(first.Modulus, second.Modulus);
        Assert.True(first.Modulus.Length >= 256, "RSA keys are at least 2048 bits.");
    }

    protected abstract ISigningKey CreateKey();

    private static bool Verify(SigningPublicKey publicKey, byte[] data, byte[] signature)
    {
        using RSA rsa = RSA.Create(new RSAParameters { Modulus = publicKey.Modulus, Exponent = publicKey.Exponent });
        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
