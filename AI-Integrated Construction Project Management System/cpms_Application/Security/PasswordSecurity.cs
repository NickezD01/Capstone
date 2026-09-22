using System.Security.Cryptography;
using System.Text;

namespace cpms_Application.Security
{
    /// <summary>
    /// Shared password hashing and policy. PBKDF2-SHA512, 210,000 iterations,
    /// 32-byte salt, 64-byte hash. Used by authentication and admin provisioning
    /// so both paths enforce identical credentials handling.
    /// </summary>
    public static class PasswordSecurity
    {
        private const int Iterations = 210_000;

        public static (byte[] Hash, byte[] Salt) CreateHash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(32);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, 64);
            return (hash, salt);
        }

        public static bool IsStrongPassword(string password) =>
            !string.IsNullOrWhiteSpace(password) && password.Length is >= 10 and <= 128 &&
            password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
    }
}
