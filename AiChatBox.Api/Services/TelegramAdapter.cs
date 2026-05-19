using System;
using System.IO;
using System.Net.Http;
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
    public class TelegramAdapter(
        IDbContextFactory<ChatDbContext> dbFactory,
        EncryptionService encryptionService,
        IHttpClientFactory httpClientFactory) : IChannelAdapter
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly HttpClient _http = httpClientFactory.CreateClient();

        public string ChannelName => "telegram";

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

            if (!root.TryGetProperty("message", out var msgProp))
                throw new ArgumentException("Payload is missing 'message' property.");

            // Avoid bot self-loops
            if (msgProp.TryGetProperty("from", out var fromProp) && fromProp.TryGetProperty("is_bot", out var botProp) && botProp.GetBoolean())
            {
                return new InboundMessage
                {
                    SenderId = "bot",
                    Text = string.Empty,
                    Channel = ChannelName
                };
            }

            var chatId = msgProp.GetProperty("chat").GetProperty("id").GetInt64().ToString();
            
            var text = string.Empty;
            if (msgProp.TryGetProperty("text", out var textProp))
            {
                text = textProp.GetString() ?? "";
            }

            Guid projectId = Guid.Empty;
            if (request.RouteValues.TryGetValue("projectId", out var val) && Guid.TryParse(val?.ToString(), out var pid))
            {
                projectId = pid;
            }

            return new InboundMessage
            {
                SenderId = chatId,
                Text = text,
                Channel = ChannelName,
                ProjectId = projectId
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
            if (settings?.Telegram == null || string.IsNullOrWhiteSpace(settings.Telegram.BotToken))
                throw new InvalidOperationException("Telegram Bot Token is missing in configuration.");

            var botToken = _encryptionService.Decrypt(settings.Telegram.BotToken);

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = new
            {
                chat_id = message.RecipientId,
                text = message.Text
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send message to Telegram. Status: {response.StatusCode}, Details: {errorMsg}");
            }
        }
    }
}
