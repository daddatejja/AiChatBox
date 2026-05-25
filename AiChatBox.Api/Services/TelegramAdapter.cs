using System;
using System.IO;
using System.Net.Http;
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

            Guid projectId = Guid.Empty;
            if (request.RouteValues.TryGetValue("projectId", out var val) && Guid.TryParse(val?.ToString(), out var pid))
            {
                projectId = pid;
            }

            // Verify Telegram Secret Token if configured
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
                if (settings?.Telegram != null && !string.IsNullOrWhiteSpace(settings.Telegram.SecretToken))
                {
                    var decryptedSecret = _encryptionService.Decrypt(settings.Telegram.SecretToken);
                    if (!string.IsNullOrEmpty(decryptedSecret))
                    {
                        var secretHeader = request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
                        if (string.IsNullOrEmpty(secretHeader))
                        {
                            throw new UnauthorizedAccessException("Missing required X-Telegram-Bot-Api-Secret-Token header.");
                        }

                        if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(decryptedSecret),
                            Encoding.UTF8.GetBytes(secretHeader)))
                        {
                            throw new UnauthorizedAccessException("Telegram secret token verification failed.");
                        }
                    }
                }
            }

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

            var senderName = string.Empty;
            if (msgProp.TryGetProperty("from", out var fromObj))
            {
                var firstName = fromObj.TryGetProperty("first_name", out var fnProp) ? fnProp.GetString() ?? "" : "";
                var lastName = fromObj.TryGetProperty("last_name", out var lnProp) ? lnProp.GetString() ?? "" : "";
                senderName = $"{firstName} {lastName}".Trim();
                if (string.IsNullOrEmpty(senderName))
                {
                    senderName = fromObj.TryGetProperty("username", out var unProp) ? unProp.GetString() ?? "" : "";
                }
            }

            var attachmentUrl = string.Empty;
            if (msgProp.TryGetProperty("photo", out var photoArray) && photoArray.GetArrayLength() > 0)
            {
                var lastPhoto = photoArray[photoArray.GetArrayLength() - 1];
                var fileId = lastPhoto.TryGetProperty("file_id", out var fProp) ? fProp.GetString() : null;
                if (!string.IsNullOrEmpty(fileId))
                {
                    attachmentUrl = await GetTelegramFileUrl(fileId, projectId);
                }
            }
            else if (msgProp.TryGetProperty("voice", out var voiceProp))
            {
                var fileId = voiceProp.TryGetProperty("file_id", out var fProp) ? fProp.GetString() : null;
                if (!string.IsNullOrEmpty(fileId))
                {
                    attachmentUrl = await GetTelegramFileUrl(fileId, projectId);
                }
            }
            else if (msgProp.TryGetProperty("document", out var docProp))
            {
                var fileId = docProp.TryGetProperty("file_id", out var fProp) ? fProp.GetString() : null;
                if (!string.IsNullOrEmpty(fileId))
                {
                    attachmentUrl = await GetTelegramFileUrl(fileId, projectId);
                }
            }

            return new InboundMessage
            {
                SenderId = chatId,
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

        private async Task<string> GetTelegramFileUrl(string fileId, Guid projectId)
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
                if (settings?.Telegram == null || string.IsNullOrWhiteSpace(settings.Telegram.BotToken))
                    return string.Empty;

                var botToken = _encryptionService.Decrypt(settings.Telegram.BotToken);
                var getFileUrl = $"https://api.telegram.org/bot{botToken}/getFile?file_id={fileId}";

                var response = await _http.GetAsync(getFileUrl);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    if (doc.RootElement.GetProperty("ok").GetBoolean())
                    {
                        var filePath = doc.RootElement.GetProperty("result").GetProperty("file_path").GetString();
                        return $"https://api.telegram.org/file/bot{botToken}/{filePath}";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve Telegram file: {ex.Message}");
            }
            return string.Empty;
        }
    }
}
