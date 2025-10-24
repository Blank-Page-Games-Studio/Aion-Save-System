// com.bpg.aion/Runtime/Util/Checksum.cs
#nullable enable
using System.Security.Cryptography;
using System.Text;

namespace BPG.Aion
{
    /// <summary>
    /// SHA-256 checksum utilities.
    /// </summary>
    public static class Checksum
    {
        public static string Sha256Hex(string text)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text);
            var hash = sha.ComputeHash(bytes);
            return ToHex(hash);
        }

        public static string Sha256HexBytes(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return ToHex(hash);
        }

        private static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
