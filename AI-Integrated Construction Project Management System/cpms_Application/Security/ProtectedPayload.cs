using System.Security.Cryptography;
using System.Text;

namespace cpms_Application.Security;

public static class ProtectedPayload
{
    public static string Protect(string plaintext, string masterSecret, string purpose)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{masterSecret}"));
        var nonce = RandomNumberGenerator.GetBytes(12);
        var input = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[input.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, input, ciphertext, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(ciphertext).ToArray());
    }

    public static string Unprotect(string payload, string masterSecret, string purpose)
    {
        var bytes = Convert.FromBase64String(payload);
        if (bytes.Length < 29) throw new CryptographicException("Invalid protected payload.");
        var key = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{masterSecret}"));
        var plaintext = new byte[bytes.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(bytes[..12], bytes[28..], bytes[12..28], plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
