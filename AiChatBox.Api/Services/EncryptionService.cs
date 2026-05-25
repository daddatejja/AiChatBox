using System.Security.Cryptography;
using System.Text;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// Provides AES-256-GCM encryption and decryption for sensitive data like API keys.
    /// The encryption key is sourced from application configuration.
    /// </summary>
    public class EncryptionService
    {
        private readonly byte[] _key;
        private const int NonceSize = 12; // AES-GCM standard nonce size
        private const int TagSize = 16;   // AES-GCM standard tag size

        public EncryptionService(IConfiguration configuration)
        {
            var keyString = configuration["Encryption:Key"];
            if (string.IsNullOrEmpty(keyString) || keyString.Length < 32)
                throw new InvalidOperationException(
                    "Encryption key must be configured in 'Encryption:Key' and be at least 32 characters long.");

            // Use SHA-256 to derive a consistent 32-byte key from the configured string
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
        }

        /// <summary>
        /// Encrypts a plaintext string. Returns a Base64 string containing the nonce, ciphertext, and tag.
        /// Returns null if the input is null or empty.
        /// </summary>
        public string? Encrypt(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return null;

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            var ciphertext = new byte[plaintextBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Format: [nonce][ciphertext][tag]
            var result = new byte[NonceSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Decrypts an encrypted Base64 string back to plaintext.
        /// Returns the original string if it is not a valid encrypted Base64 string or decryption fails.
        /// Returns null if the input is null or empty.
        /// </summary>
        public string? Decrypt(string? encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return null;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedBase64);

                if (encryptedBytes.Length < NonceSize + TagSize)
                    return encryptedBase64;

                var nonce = new byte[NonceSize];
                var ciphertextLength = encryptedBytes.Length - NonceSize - TagSize;
                var ciphertext = new byte[ciphertextLength];
                var tag = new byte[TagSize];

                Buffer.BlockCopy(encryptedBytes, 0, nonce, 0, NonceSize);
                Buffer.BlockCopy(encryptedBytes, NonceSize, ciphertext, 0, ciphertextLength);
                Buffer.BlockCopy(encryptedBytes, NonceSize + ciphertextLength, tag, 0, TagSize);

                var plaintext = new byte[ciphertextLength];

                using var aes = new AesGcm(_key, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                return Encoding.UTF8.GetString(plaintext);
            }
            catch
            {
                return encryptedBase64;
            }
        }
    }
}
