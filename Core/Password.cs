namespace Application.Core;

using System.Text;

/// <summary>
/// Provides password hashing and verification utilities using HMAC-SHA512.
/// </summary>
public static class Password
{
    /// <summary>
    /// Creates a password hash and salt using HMAC-SHA512.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>
    /// A tuple containing the generated password salt and hash.
    /// </returns>
    public static (byte[] passwordSalt, byte[] passwordHash) Create(string password)
    {
        // HMACSHA512 generates a secure random key on instantiation, which we use as the salt
        using var hmac = new System.Security.Cryptography.HMACSHA512();
        byte[] passwordSalt = hmac.Key;
        byte[] passwordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return (passwordSalt, passwordHash);
    }

    /// <summary>
    /// Verifies that a given plaintext password matches the stored hash and salt.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="storedHash">The previously computed hash to compare against.</param>
    /// <param name="storedSalt">The salt originally used to compute the hash.</param>
    /// <returns>
    /// True if the password is valid; otherwise, false.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the length of the provided hash or salt is invalid.
    /// </exception>
    public static bool Verify(string password, byte[] storedHash, byte[] storedSalt)
    {
        if (storedHash.Length != 64)
            throw new ArgumentException("Invalid length of password hash (64 bytes expected).", nameof(storedHash));
        if (storedSalt.Length != 128)
            throw new ArgumentException("Invalid length of password salt (128 bytes expected).", nameof(storedSalt));

        // Recompute the hash using the provided salt (HMAC key) and input password
        using var hmac = new System.Security.Cryptography.HMACSHA512(storedSalt);
        byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

        // Compare each byte to avoid timing attacks (constant time comparison not used here but byte-by-byte)
        return !computedHash.Where((t, i) => t != storedHash[i]).Any();
    }
}
