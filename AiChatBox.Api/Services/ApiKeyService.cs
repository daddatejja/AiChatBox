using System.Security.Cryptography;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class ApiKeyService(ChatDbContext db)
    {
        private readonly ChatDbContext _db = db;

        public async Task<(string Key, ApiKey Entity)> GenerateApiKeyAsync(Guid projectId, string? label = null)
        {
            var rawKey = GenerateSecureKey();
            var keyHash = HashKey(rawKey);

            var apiKey = new ApiKey
            {
                ProjectId = projectId,
                KeyHash = keyHash,
                Label = label,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.ApiKeys.Add(apiKey);
            await _db.SaveChangesAsync();

            return (rawKey, apiKey);
        }

        public async Task<Project?> ValidateApiKeyAsync(string rawKey)
        {
            var keyHash = HashKey(rawKey);
            var apiKey = await _db.ApiKeys
                .Include(k => k.Project)
                .ThenInclude(p => p.CustomTools)
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

            if (apiKey != null)
            {
                apiKey.LastUsedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return apiKey.Project;
            }

            return null;
        }

        private string GenerateSecureKey()
        {
            var buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);
            return "acb_" + Convert.ToBase64String(buffer)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")
                .Substring(0, 32);
        }

        private string HashKey(string key)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
