using System.Security.Cryptography;

namespace ProjectA.Api.Security;

// Deliberately dependency-free (no ASP.NET Core Identity package) - this project already
// hand-rolls its data access with Dapper rather than EF Core, so pulling in the full Identity
// stack just for password hashing would be a bigger dependency than the feature needs.
// PBKDF2-HMACSHA256 via the BCL's Rfc2898DeriveBytes is a well-vetted, standard choice.
internal static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 210_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    // Stored as "{iterations}.{base64 salt}.{base64 key}" so the work factor and salt travel
    // with the hash - future callers can re-hash with a higher iteration count without a
    // separate migration, and verification never needs to guess which parameters were used.
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySizeBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedKey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);

        // Constant-time comparison - a timing difference here would leak how many leading
        // bytes of the hash matched, which is exactly what you don't want for a password check.
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
