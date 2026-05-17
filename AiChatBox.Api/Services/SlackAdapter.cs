using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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

            Guid projectId = Guid.Empty;
            if (request.RouteValues.TryGetValue("projectId", out var val) && Guid.TryParse(val?.ToString(), out var pid))
            {
                projectId = pid;
            }

            return new InboundMessage
            {
                SenderId = senderId,
                Text = text,
                Channel = ChannelName,
                SessionExternalId = !string.IsNullOrEmpty(threadTs) ? $"{channelId}:{threadTs}" : channelId,
                ProjectId = projectId
            };
        }

        public async Task SendOutbound(OutboundMessage message)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var config = await db.Configurations
                .FirstOrDefaultAsync(c => c.ProjectId == message.ProjectId && c.Name == "Default");

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
    }
}
