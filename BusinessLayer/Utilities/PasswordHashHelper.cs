namespace BusinessLayer.Utilities;

public static class PasswordHashHelper
{
    public static string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        return BCrypt.Net.BCrypt.HashPassword(password.Trim());
    }

    public static bool IsBcryptHash(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        return stored.StartsWith("$2", StringComparison.Ordinal) && stored.Length >= 29;
    }

    /// <summary>
    /// Verifies password against BCrypt hash, or legacy plaintext stored in PasswordHash (pre-fix Users API).
    /// </summary>
    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        var plain = (password ?? string.Empty).Trim();
        if (plain.Length == 0) return false;

        if (IsBcryptHash(storedHash))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(plain, storedHash);
            }
            catch
            {
                return false;
            }
        }

        return string.Equals(plain, storedHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// If legacy data stored the plain password in PasswordHash, upgrade to BCrypt (same plaintext as input).
    /// </summary>
    public static string NormalizeToBcrypt(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return stored ?? string.Empty;
        if (IsBcryptHash(stored))
            return stored;
        return Hash(stored);
    }

    /// <summary>True when stored value is legacy plaintext (not BCrypt).</summary>
    public static bool IsLegacyPlaintext(string? stored) =>
        !string.IsNullOrEmpty(stored) && !IsBcryptHash(stored);
}
