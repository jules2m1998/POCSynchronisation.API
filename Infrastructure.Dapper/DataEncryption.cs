using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Dapper;

public static class DataEncryption
{
    private static readonly byte[] Key = GetEncryptionKey();
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("your-16-char-iv!"); // 16 bytes for AES

    private static byte[] GetEncryptionKey()
    {
        // In production, get from secure configuration
        var keyString = "your-32-character-secret-key!!";
        return Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            // If encryption fails, return original (you might want to log this)
            return plainText;
        }
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var encryptedBytes = Convert.FromBase64String(cipherText);
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            // If decryption fails, return original (data might not be encrypted)
            return cipherText;
        }
    }

    public static bool IsEncrypted(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}