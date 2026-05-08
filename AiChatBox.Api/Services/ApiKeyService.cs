using System.Security.Cryptography;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class ApiKeyService(ChatDbContext db)
    {
        private readonly ChatDbContext _db = db;

        public async Task<(string Key, ApiKey Entity)> GenerateApiKeyAsync(Guid projectId, string? label = null, Guid? configurationId = null)
        {
            var rawKey = GenerateSecureKey();
            var keyHash = HashKey(rawKey);

            var apiKey = new ApiKey
            {
                ProjectId = projectId,
                ConfigurationId = configurationId,
                KeyHash = keyHash,
                Label = label,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _db.ApiKeys.Add(apiKey);
            await _db.SaveChangesAsync();

            return (rawKey, apiKey);
        }

        public async Task<(Project? Project, ProjectConfiguration? Configuration, ApiKey? ApiKey)> ValidateApiKeyAsync(string rawKey, string? origin = null)
        {
            var keyHash = HashKey(rawKey);
            var apiKey = await _db.ApiKeys
                .Include(k => k.Project)
                    .ThenInclude(p => p.CustomTools)
                .Include(k => k.Configuration)
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

            if (apiKey != null)
            {
                // Validate domain if whitelisting is enabled
                if (!string.IsNullOrEmpty(apiKey.Project?.AllowedDomains))
                {
                    var allowed = apiKey.Project.AllowedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    if (!allowed.Contains("*"))
                    {
                        if (string.IsNullOrEmpty(origin))
                        {
                            // Origin missing but required
                            return (null, null, null);
                        }

                        string originAuthority;
                        try { originAuthority = new Uri(origin).Authority; }
                        catch { originAuthority = origin; }

                        bool isAllowed = false;
                        foreach (var d in allowed)
                        {
                            string allowedAuthority;
                            if (d.Contains("://"))
                            {
                                try { allowedAuthority = new Uri(d).Authority; }
                                catch { allowedAuthority = d; }
                            }
                            else
                            {
                                allowedAuthority = d;
                            }

                            // Wildcard match (*.example.com)
                            if (allowedAuthority.StartsWith("*."))
                            {
                                var domain = allowedAuthority.Substring(2);
                                var originHost = originAuthority.Contains(':') ? originAuthority.Split(':')[0] : originAuthority;
                                if (originHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) || 
                                    originHost.Equals(domain, StringComparison.OrdinalIgnoreCase))
                                {
                                    isAllowed = true;
                                    break;
                                }
                            }
                            // Exact match
                            else if (originAuthority.Equals(allowedAuthority, StringComparison.OrdinalIgnoreCase))
                            {
                                isAllowed = true;
                                break;
                            }
                            // Host match ignoring port (if allowedAuthority has no port)
                            else if (!allowedAuthority.Contains(':') && originAuthority.Contains(':'))
                            {
                                var originHost = originAuthority.Split(':')[0];
                                if (originHost.Equals(allowedAuthority, StringComparison.OrdinalIgnoreCase))
                                {
                                    isAllowed = true;
                                    break;
                                }
                            }
                        }

                        if (!isAllowed)
                        {
                            return (null, null, null);
                        }
                    }
                }

                apiKey.LastUsedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return (apiKey.Project, apiKey.Configuration, apiKey);
            }

            return (null, null, null);
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
