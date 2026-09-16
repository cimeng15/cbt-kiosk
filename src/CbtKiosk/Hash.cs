using System.Security.Cryptography;
using System.Text;

namespace CbtKiosk;

public static class Hash
{
    /// <summary>Lower-case, base-16 (hex) SHA-256 of the UTF-8 bytes of <paramref name="value"/>.</summary>
    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>Length-safe, constant-time string comparison (for passwords / hashes).</summary>
    public static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
