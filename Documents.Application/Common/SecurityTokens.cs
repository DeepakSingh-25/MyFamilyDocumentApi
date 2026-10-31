using System.Security.Cryptography;
using System.Text;

namespace Documents.Application.Common
{
    internal static class SecurityTokens
    {
        /// <summary>32 random bytes, hex (URL-safe).</summary>
        public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

        /// <summary>SHA-256 hex — DB mein sirf yahi store hota hai.</summary>
        public static string Sha256Hex(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

        /// <summary>Constant-time compare (lambai bhi leak na ho, isliye pehle hash).</summary>
        public static bool FixedTimeEquals(string a, string b) =>
            CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(a)),
                SHA256.HashData(Encoding.UTF8.GetBytes(b)));
    }
}
