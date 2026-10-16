using System.Security.Cryptography;

namespace SmartTorque.Services;

/// <summary>
/// Cryptographically secure token generator for individual product unit tracking.
/// Every physical unit produced is assigned a unique token of strictly 40 characters.
/// </summary>
public static class TokenGenerator
{
    private const string Prefix = "PRD-"; // 4 chars
    private const int RandomBytesCount = 18; // 18 bytes * 2 = 36 hex chars. 4 + 36 = 40 chars total.

    /// <summary>
    /// Generates a globally unique 40-character tracking token for an individual product unit.
    /// Format: "PRD-" followed by 36 uppercase hex characters (strictly 40 chars).
    /// </summary>
    public static string GenerateProductUnitToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RandomBytesCount);
        var hex = Convert.ToHexString(bytes); // 36 chars
        var token = $"{Prefix}{hex}";
        if (token.Length != 40)
        {
            throw new InvalidOperationException($"Generated token length {token.Length} does not match required 40 characters.");
        }
        return token;
    }

    /// <summary>
    /// Generates a secure API key of standard format with a given prefix.
    /// </summary>
    public static string GenerateApiKey(string prefix = "st_live_")
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        return $"{prefix}{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
