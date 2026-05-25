using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class SlackAdapter(
        IDbContextFactory<ChatDbContext> dbFactory,
        EncryptionService encryptionService,
        IHttpClientFactory httpClientFactory) : IChannelAdapter
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly HttpClient _http = httpClientFactory.CreateClient();

        public string ChannelName => "slack";

        public async Task<InboundMessage> ParseInbound(HttpRequest request)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(bodyText))
                throw new ArgumentException("Request body is empty.");

            Guid projectId = Guid.Empty;
            if (request.RouteValues.TryGetValue("projectId", out var val) && Guid.TryParse(val?.ToString(), out var pid))
            {
                projectId = pid;
            }

            // Verify Slack Signature if configured
            await using var db = await _dbFactory.CreateDbContextAsync();
            var config = await db.Configurations
                .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Name == "Default");
            if (config == null)
            {
                config = await db.Configurations
                    .FirstOrDefaultAsync(c => c.ProjectId == projectId);
            }

            if (config != null && !string.IsNullOrWhiteSpace(config.ChannelSettingsJson))
            {
                var settings = JsonSerializer.Deserialize<ChannelSettings>(config.ChannelSettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (settings?.Slack != null && !string.IsNullOrWhiteSpace(settings.Slack.SigningSecret))
                {
                    var decryptedSecret = _encryptionService.Decrypt(settings.Slack.SigningSecret);
                    if (!string.IsNullOrEmpty(decryptedSecret))
                    {
                        var timestampHeader = request.Headers["X-Slack-Request-Timestamp"].ToString();
                        var signatureHeader = request.Headers["X-Slack-Signature"].ToString();

                        if (string.IsNullOrEmpty(timestampHeader) || string.IsNullOrEmpty(signatureHeader))
                        {
                            throw new UnauthorizedAccessException("Missing required Slack signature headers.");
                        }

                        if (!long.TryParse(timestampHeader, out var timestamp))
                        {
                            throw new UnauthorizedAccessException("Invalid Slack request timestamp format.");
                        }

                        var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        if (Math.Abs(currentUnixTime - timestamp) > 300)
                        {
                            throw new UnauthorizedAccessException("Slack request timestamp has expired (replay attack protection).");
                        }

                        var baseString = $"v0:{timestampHeader}:{bodyText}";
                        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(decryptedSecret));
                        var computedHashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
                        var computedSignature = $"v0={Convert.ToHexString(computedHashBytes).ToLowerInvariant()}";

                        if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(computedSignature),
                            Encoding.UTF8.GetBytes(signatureHeader)))
                        {
                            throw new UnauthorizedAccessException("Slack request signature verification failed.");
                        }
                    }
                }
            }

            using var doc = JsonDocument.Parse(bodyText);
            var root = doc.RootElement;

            // Handle URL Verification handshake
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "url_verification")
            {
                var challenge = root.GetProperty("challenge").GetString() ?? "";
                return new InboundMessage
                {
                    SenderId = "slack_system",
                    Text = challenge, // Echo challenge
                    Channel = ChannelName,
                    SessionExternalId = "handshake"
                };
            }

            if (!root.TryGetProperty("event", out var evProp))
                throw new ArgumentException("Invalid Slack event payload.");

            // Ignore messages from bots to prevent infinite response loops
            if (evProp.TryGetProperty("bot_id", out _) || evProp.TryGetProperty("subtype", out var subtypeProp) && subtypeProp.GetString() == "bot_message")
            {
                return new InboundMessage
                {
                    SenderId = "bot",
                    Text = string.Empty,
                    Channel = ChannelName
                };
            }

            var senderId = evProp.GetProperty("user").GetString() ?? "";
            var text = evProp.GetProperty("text").GetString() ?? "";
            var channelId = evProp.GetProperty("channel").GetString() ?? ""; // Used as the external session ID

            // Slack threads: check if message was posted in a thread
            var threadTs = string.Empty;
            if (evProp.TryGetProperty("thread_ts", out var tProp))
            {
                threadTs = tProp.GetString() ?? "";
            }

            var senderName = await GetSlackUserName(senderId, projectId);

            var attachmentUrl = string.Empty;
            if (evProp.TryGetProperty("files", out var filesProp) && filesProp.GetArrayLength() > 0)
            {
                var firstFile = filesProp[0];
                attachmentUrl = firstFile.TryGetProperty("url_private", out var upProp) ? upProp.GetString() ?? "" : "";
            }

            return new InboundMessage
            {
                SenderId = senderId,
                Text = text,
                Channel = ChannelName,
                SessionExternalId = !string.IsNullOrEmpty(threadTs) ? $"{channelId}:{threadTs}" : channelId,
                ProjectId = projectId,
                SenderName = senderName,
                AttachmentUrl = attachmentUrl
            };
        }

        public async Task SendOutbound(OutboundMessage message)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var config = await db.Configurations
                .FirstOrDefaultAsync(c => c.ProjectId == message.ProjectId && c.Name == "Default");
            if (config == null)
            {
                config = await db.Configurations
                    .FirstOrDefaultAsync(c => c.ProjectId == message.ProjectId);
            }

            if (config == null || string.IsNullOrWhiteSpace(config.ChannelSettingsJson))
                throw new InvalidOperationException("No configuration with channel settings found for this project.");

            var settings = JsonSerializer.Deserialize<ChannelSettings>(config.ChannelSettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (settings?.Slack == null || string.IsNullOrWhiteSpace(settings.Slack.BotToken))
                throw new InvalidOperationException("Slack Bot Token is missing in configuration.");

            var botToken = _encryptionService.Decrypt(settings.Slack.BotToken);

            var url = "https://slack.com/api/chat.postMessage";

            // Parse channel and optional thread_ts from RecipientId (or external session ID)
            var parts = message.RecipientId.Split(':');
            var channel = parts[0];
            string? threadTs = parts.Length > 1 ? parts[1] : null;

            var payload = new
            {
                channel,
                text = message.Text,
                thread_ts = threadTs
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", botToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send message to Slack. Details: {errorMsg}");
            }

            var respText = await response.Content.ReadAsStringAsync();
            using var respDoc = JsonDocument.Parse(respText);
            if (!respDoc.RootElement.GetProperty("ok").GetBoolean())
            {
                var error = respDoc.RootElement.GetProperty("error").GetString();
                throw new Exception($"Slack API returned error: {error}");
            }
        }

        private async Task<string> GetSlackUserName(string userId, Guid projectId)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var config = await db.Configurations
                    .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Name == "Default");
                if (config == null)
                {
                    config = await db.Configurations
                        .FirstOrDefaultAsync(c => c.ProjectId == projectId);
                }

                if (config == null || string.IsNullOrWhiteSpace(config.ChannelSettingsJson))
                    return string.Empty;

                var settings = JsonSerializer.Deserialize<ChannelSettings>(config.ChannelSettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (settings?.Slack == null || string.IsNullOrWhiteSpace(settings.Slack.BotToken))
                    return string.Empty;

                var botToken = _encryptionService.Decrypt(settings.Slack.BotToken);
                var url = $"https://slack.com/api/users.info?user={userId}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", botToken);

                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;
                    if (root.GetProperty("ok").GetBoolean() && root.TryGetProperty("user", out var userObj))
                    {
                        return userObj.GetProperty("profile").GetProperty("real_name").GetString() ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve Slack user name: {ex.Message}");
            }
            return string.Empty;
        }
    }
}
