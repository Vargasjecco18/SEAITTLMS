using System.Security.Cryptography;

namespace SEAITT_LMS.Auth;

/// <summary>
/// Password hashing with PBKDF2-HMAC-SHA256 from the .NET base library, no Identity package needed.
/// Stored format: PBKDF2-SHA256$iterations$salt(base64)$hash(base64). The iteration count is stored
/// with the hash so it can be raised later and old hashes upgraded on the next successful login.
/// </summary>
public sealed class PasswordHasher
{
    private const string Algorithm = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaxAcceptedIterations = 5_000_000;

    /// <summary>OWASP's recommended minimum for PBKDF2-HMAC-SHA256.</summary>
    public const int CurrentIterations = 600_000;

    private readonly Lazy<string> _dummyHash;

    public PasswordHasher()
    {
        // Used to spend the same time when the email is unknown, so response times don't reveal which emails exist.
        _dummyHash = new Lazy<string>(() => Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))));
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, CurrentIterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Algorithm}${CurrentIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        if (!TryParse(storedHash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>True when the hash was made with fewer iterations than we use today (or can't be read).</summary>
    public bool NeedsRehash(string storedHash) =>
        !TryParse(storedHash, out var iterations, out _, out _) || iterations < CurrentIterations;

    /// <summary>Does the same work as <see cref="Verify"/> against a throw-away hash. Always returns false.</summary>
    public void BurnTime(string password) => Verify(password, _dummyHash.Value);

    private static bool TryParse(string? stored, out int iterations, out byte[] salt, out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = stored?.Split('$');
        if (parts is not { Length: 4 } || parts[0] != Algorithm)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out iterations) || iterations < 1 || iterations > MaxAcceptedIterations)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length >= 8 && hash.Length >= 16;
    }
}
