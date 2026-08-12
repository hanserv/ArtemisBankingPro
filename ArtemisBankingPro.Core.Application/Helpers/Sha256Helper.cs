using System.Security.Cryptography;
using System.Text;

namespace ArtemisBankingPro.Core.Application.Helpers
{
    public static class Sha256Helper
    {
        public static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        public static bool Verify(string value, string hash)
        {
            return Hash(value) == hash;
        }
    }
}
