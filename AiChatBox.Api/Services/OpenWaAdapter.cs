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
    public class OpenWaAdapter(
        IDbContextFactory<ChatDbContext> dbFactory,
        EncryptionService encryptionService,
        IHttpClientFactory httpClientFactory) : IChannelAdapter
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly HttpClient _http = httpClientFactory.CreateClient();

        public string ChannelName => "openwa";

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

            // OpenWA webhooks wrap standard events under event and data properties
            JsonElement target = root;
            if (root.TryGetProperty("data", out var dataProp))
            {
                target = dataProp;
            }

            // Retrieve senderId (from field, e.g. 628123456789@c.us)
            var senderId = string.Empty;
            if (target.TryGetProperty("from", out var fromProp))
            {
                senderId = fromProp.GetString() ?? "";
            }

            if (string.IsNullOrEmpty(senderId))
            {
                return new InboundMessage
                {
                    SenderId = "bot",
                    Text = string.Empty,
                    Channel = ChannelName
                };
            }

            // Avoid bot self-loops (OpenWA messages from ourselves have fromMe: true)
            if (target.TryGetProperty("fromMe", out var meProp) && meProp.GetBoolean())
            {
                return new InboundMessage
                {
                    SenderId = "bot",
                    Text = string.Empty,
                    Channel = ChannelName
                };
            }

            // Retrieve text content (body field)
            var text = string.Empty;
            if (target.TryGetProperty("body", out var bodyProp))
            {
                text = bodyProp.GetString() ?? "";
            }
            else if (target.TryGetProperty("text", out var textProp))
            {
                text = textProp.GetString() ?? "";
            }

            // Parse sender profile name (from sender.pushname or sender.name)
            var senderName = string.Empty;
            if (target.TryGetProperty("sender", out var senderProp))
            {
                if (senderProp.TryGetProperty("pushname", out var pushnameProp))
                    senderName = pushnameProp.GetString() ?? "";
                else if (senderProp.TryGetProperty("name", out var nameProp))
                    senderName = nameProp.GetString() ?? "";
            }

            // Parse file/media attachment URL if any (clientUrl or url)
            var attachmentUrl = string.Empty;
            if (target.TryGetProperty("clientUrl", out var urlProp1))
            {
                attachmentUrl = urlProp1.GetString() ?? "";
            }
            else if (target.TryGetProperty("url", out var urlProp2))
            {
                attachmentUrl = urlProp2.GetString() ?? "";
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
            if (settings?.OpenWa == null || string.IsNullOrWhiteSpace(settings.OpenWa.InstanceUrl) || string.IsNullOrWhiteSpace(settings.OpenWa.SessionName))
                throw new InvalidOperationException("OpenWA settings are incomplete in the configuration.");

            var instanceUrl = settings.OpenWa.InstanceUrl.TrimEnd('/');
            var sessionName = settings.OpenWa.SessionName;
            var apiKey = !string.IsNullOrEmpty(settings.OpenWa.ApiKey) ? _encryptionService.Decrypt(settings.OpenWa.ApiKey) : string.Empty;

            // Endpoint: POST /api/sessions/{sessionName}/messages/send-text
            var url = $"{instanceUrl}/api/sessions/{sessionName}/messages/send-text";

            var payload = new
            {
                chatId = message.RecipientId,
                text = message.Text
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("X-API-Key", apiKey);
            }
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send message over OpenWA. Status: {response.StatusCode}, Details: {errorMsg}");
            }
        }
    }
}
