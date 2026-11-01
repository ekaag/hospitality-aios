namespace HospitalityOS.Ports.Security;

/// <summary>
/// A private key that signs the tokens the API issues itself, such as guests' stay tokens (K4c), with RS256
/// (RSASSA-PKCS1-v1_5 over SHA-256). The app never holds the private key where a vault keeps it: it asks the key to sign,
/// and checks signatures with the public key. Bound through <c>Bindings:SigningKey</c>; instances are shared, so
/// adapters must be thread-safe.
/// </summary>
public interface ISigningKey
{
    /// <summary>
    /// The public key signatures are checked with, and the ID tokens name it by (<c>kid</c>), which changes when the key
    /// is rotated.
    /// </summary>
    Task<SigningPublicKey> GetPublicKeyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The RS256 signature of <paramref name="data"/>, by the key <see cref="GetPublicKeyAsync"/> describes.
    /// </summary>
    /// <returns>The signature and the ID of the key that made it.</returns>
    Task<Signature> SignAsync(byte[] data, CancellationToken cancellationToken);
}

/// <summary>
/// An RSA public key: its ID, modulus, and exponent (big-endian).
/// </summary>
public sealed record SigningPublicKey(string KeyId, byte[] Modulus, byte[] Exponent);

/// <summary>
/// A signature and the ID of the key that made it.
/// </summary>
public sealed record Signature(string KeyId, byte[] Value);
