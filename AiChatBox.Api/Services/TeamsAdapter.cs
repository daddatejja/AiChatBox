using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class TeamsAdapter(
        IDbContextFactory<ChatDbContext> dbFactory,
        EncryptionService encryptionService,
        IHttpClientFactory httpClientFactory) : IChannelAdapter
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly HttpClient _http = httpClientFactory.CreateClient();

        // High-performance thread-safe cache for Microsoft Bot Framework access tokens
        private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> _tokenCache = new();

        public string ChannelName => "teams";

        public async Task<InboundMessage> ParseInbound(HttpRequest request)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(bodyText))
                throw new ArgumentException("Request body is empty.");

            using var doc = JsonDocument.Parse(bodyText);
            var root = doc.RootElement;

            // Microsoft Bot Framework Activities must be of type "message"
            if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "message")
            {
                return new InboundMessage
                {
                    SenderId = "bot", // Handled by controller to return Ok() and ignore further logic
                    Text = string.Empty,
                    Channel = ChannelName
                };
            }

            var senderId = root.GetProperty("from").GetProperty("id").GetString() ?? "";
            var rawText = root.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";
            
            // Clean bot mention tags (<at>AiChatBox</at>) from the prompt to avoid AI confusion
            var text = Regex.Replace(rawText, @"<at>.*?</at>", "").Trim();

            var conversationId = root.GetProperty("conversation").GetProperty("id").GetString() ?? "";
            var serviceUrl = root.TryGetProperty("serviceUrl", out var sUrlProp) ? sUrlProp.GetString() ?? "" : "";

            Guid projectId = Guid.Empty;
            if (request.RouteValues.TryGetValue("projectId", out var val) && Guid.TryParse(val?.ToString(), out var pid))
            {
                projectId = pid;
            }

            // Pack conversation ID and service URL statelessly using a pipe delimiter
            var sessionExternalId = $"{conversationId}|{serviceUrl}";

            return new InboundMessage
            {
                SenderId = senderId,
                Text = text,
                Channel = ChannelName,
                SessionExternalId = sessionExternalId,
                ProjectId = projectId
            };
        }

        public async Task SendOutbound(OutboundMessage message)
        {
            // Unpack conversation ID and service URL from recipient state
            var parts = message.RecipientId.Split('|');
            if (parts.Length < 2)
            {
                throw new ArgumentException("Teams RecipientId must be packed in the format: conversationId|serviceUrl");
            }

            var conversationId = parts[0];
            var serviceUrl = parts[1].TrimEnd('/');

            await using var db = await _dbFactory.CreateDbContextAsync();
            var config = await db.Configurations
                .FirstOrDefaultAsync(c => c.ProjectId == message.ProjectId && c.Name == "Default");
            if (config == null)
            {
                config = await db.Configurations
                    .FirstOrDefaultAsync(c => c.ProjectId == message.ProjectId);
            }

            if (config == null || string.IsNullOrWhiteSpace(config.ChannelSettingsJson))
                throw new InvalidOperationException("Project configuration settings are missing.");

            var settings = JsonSerializer.Deserialize<ChannelSettings>(config.ChannelSettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (settings?.Teams == null || string.IsNullOrWhiteSpace(settings.Teams.AppId) || string.IsNullOrWhiteSpace(settings.Teams.AppPassword))
                throw new InvalidOperationException("Microsoft Teams bot credentials (AppId / AppPassword) are not configured.");

            var appId = settings.Teams.AppId;
            var appPassword = _encryptionService.Decrypt(settings.Teams.AppPassword);

            // Fetch or refresh the cached token
            var token = await GetAccessTokenAsync(appId, appPassword);

            var postUrl = $"{serviceUrl}/v3/conversations/{Uri.EscapeDataString(conversationId)}/activities";

            var payload = new
            {
                type = "message",
                text = message.Text
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, postUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to dispatch message to Teams Bot API. Status: {response.StatusCode}. Details: {errorDetails}");
            }
        }

        private async Task<string> GetAccessTokenAsync(string appId, string appPassword)
        {
            var cacheKey = appId;

            if (_tokenCache.TryGetValue(cacheKey, out var cache) && cache.ExpiresAt > DateTime.UtcNow.AddMinutes(2))
            {
                return cache.Token;
            }

            var loginUrl = "https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token";
            
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", appId),
                new KeyValuePair<string, string>("client_secret", appPassword),
                new KeyValuePair<string, string>("scope", "https://api.botframework.com/.default")
            });

            using var response = await _http.PostAsync(loginUrl, formContent);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Microsoft OAuth2 login handshake failed. Details: {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString() ?? "";
            var expiresInSeconds = root.GetProperty("expires_in").GetInt32();

            var expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
            _tokenCache[cacheKey] = (accessToken, expiresAt);

            return accessToken;
        }
    }
}
