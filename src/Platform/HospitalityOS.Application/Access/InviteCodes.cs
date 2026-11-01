using System.Security.Cryptography;
using System.Text;

namespace HospitalityOS.Application.Access;

/// <summary>
/// Access codes, for staff invites and guests' check-in links: 20 Crockford base32 characters (100 random bits) shown in groups of five, e.g.
/// <c>K7QF3-MZP9A-4WX2C-H8RTD</c>. Only the SHA-256 hash of the normalized code is stored, so the code works once and is
/// never readable afterwards. Normalizing drops hyphens and spaces, ignores case, and reads O as 0 and I or L as 1, so
/// a code read aloud or retyped still matches.
/// </summary>
internal static class InviteCodes
{
    /// <summary>
    /// How long a new invite stays open.
    /// </summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromDays(7);

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 20;
    private const int GroupLength = 5;

    /// <summary>
    /// A new code and its hash.
    /// </summary>
    public static (string Code, string Hash) New()
    {
        StringBuilder code = new(Length + (Length / GroupLength));

        for (int i = 0; i < Length; i++)
        {
            if (i > 0 && i % GroupLength == 0)
            {
                code.Append('-');
            }

            code.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }

        string shown = code.ToString();
        return (shown, Hash(shown)!);
    }

    /// <summary>
    /// The hash a code is stored under, or <c>null</c> when it cannot be a code (so nothing is looked up).
    /// </summary>
    public static string? Hash(string? code)
    {
        if (code is null)
        {
            return null;
        }

        StringBuilder normalized = new(Length);

        foreach (char c in code)
        {
            if (c is '-' or ' ')
            {
                continue;
            }

            char upper = char.ToUpperInvariant(c) switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                char other => other,
            };

            if (!Alphabet.Contains(upper, StringComparison.Ordinal) || normalized.Length == Length)
            {
                return null;
            }

            normalized.Append(upper);
        }

        return normalized.Length == Length
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(normalized.ToString())))
            : null;
    }
}
