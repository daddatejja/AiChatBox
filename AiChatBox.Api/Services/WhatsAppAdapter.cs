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
    public class WhatsAppAdapter(
        IDbContextFactory<ChatDbContext> dbFactory,
        EncryptionService encryptionService,
        IHttpClientFactory httpClientFactory) : IChannelAdapter
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly HttpClient _http = httpClientFactory.CreateClient();

        public string ChannelName => "whatsapp";

        public async Task<InboundMessage> ParseInbound(HttpRequest request)
        {
            // Read body stream
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var bodyText = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Reset for downstream components

            if (string.IsNullOrWhiteSpace(bodyText))
                throw new ArgumentException("Request body is empty.");

            Guid projectId = Guid.Empty;
            if (request.RouteValues.TryGetValue("projectId", out var val) && Guid.TryParse(val?.ToString(), out var pid))
            {
                projectId = pid;
            }

            // Verify WhatsApp Signature if AppSecret is configured
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
                if (settings?.WhatsApp != null && !string.IsNullOrWhiteSpace(settings.WhatsApp.AppSecret))
                {
                    var decryptedSecret = _encryptionService.Decrypt(settings.WhatsApp.AppSecret);
                    if (!string.IsNullOrEmpty(decryptedSecret))
                    {
                        var signatureHeader = request.Headers["X-Hub-Signature-256"].ToString();
                        if (string.IsNullOrEmpty(signatureHeader))
                        {
                            throw new UnauthorizedAccessException("Missing required X-Hub-Signature-256 header.");
                        }

                        const string prefix = "sha256=";
                        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new UnauthorizedAccessException("Invalid X-Hub-Signature-256 header format.");
                        }

                        var actualSignature = signatureHeader.Substring(prefix.Length);

                        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(decryptedSecret));
                        var computedHashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(bodyText));
                        var computedSignature = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

                        if (!CryptographicOperations.FixedTimeEquals(
                            Encoding.UTF8.GetBytes(computedSignature),
                            Encoding.UTF8.GetBytes(actualSignature)))
                        {
                            throw new UnauthorizedAccessException("WhatsApp request signature verification failed.");
                        }
                    }
                }
            }

            using var doc = JsonDocument.Parse(bodyText);
            var root = doc.RootElement;

            // Navigate WhatsApp Business API Webhook payload structure
            if (!root.TryGetProperty("entry", out var entryArray) || entryArray.GetArrayLength() == 0)
                throw new ArgumentException("Invalid WhatsApp payload structure: missing 'entry'.");

            var firstEntry = entryArray[0];
            if (!firstEntry.TryGetProperty("changes", out var changesArray) || changesArray.GetArrayLength() == 0)
                throw new ArgumentException("Invalid WhatsApp payload structure: missing 'changes'.");

            var firstChange = changesArray[0];
            if (!firstChange.TryGetProperty("value", out var valProp))
                throw new ArgumentException("Invalid WhatsApp payload structure: missing 'value'.");

            if (!valProp.TryGetProperty("messages", out var messagesArray) || messagesArray.GetArrayLength() == 0)
                throw new ArgumentException("No messages found in changes value.");

            var messageObj = messagesArray[0];
            var senderId = messageObj.GetProperty("from").GetString() ?? "";
            
            var text = string.Empty;
            if (messageObj.TryGetProperty("text", out var textProp))
            {
                text = textProp.GetProperty("body").GetString() ?? "";
            }
            else if (messageObj.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "interactive")
            {
                // Handle quick replies or buttons if needed
                if (messageObj.TryGetProperty("interactive", out var interProp))
                {
                    if (interProp.TryGetProperty("button_reply", out var btnProp))
                        text = btnProp.GetProperty("title").GetString() ?? "";
                }
            }

            var senderName = string.Empty;
            if (valProp.TryGetProperty("contacts", out var contactsArray) && contactsArray.GetArrayLength() > 0)
            {
                var contactObj = contactsArray[0];
                if (contactObj.TryGetProperty("profile", out var profileProp))
                {
                    senderName = profileProp.GetProperty("name").GetString() ?? "";
                }
            }

            var attachmentUrl = string.Empty;
            if (messageObj.TryGetProperty("type", out var msgTypeProp))
            {
                var typeStr = msgTypeProp.GetString();
                if (typeStr == "image" || typeStr == "audio" || typeStr == "voice" || typeStr == "document" || typeStr == "video")
                {
                    if (messageObj.TryGetProperty(typeStr, out var mediaObj))
                    {
                        var mediaId = mediaObj.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        if (!string.IsNullOrEmpty(mediaId))
                        {
                            attachmentUrl = await GetWhatsAppMediaUrl(mediaId, projectId);
                        }
                    }
                }
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
            if (settings?.WhatsApp == null || string.IsNullOrWhiteSpace(settings.WhatsApp.AccessToken) || string.IsNullOrWhiteSpace(settings.WhatsApp.PhoneNumberId))
                throw new InvalidOperationException("WhatsApp settings are incomplete in the configuration.");

            var accessToken = _encryptionService.Decrypt(settings.WhatsApp.AccessToken);
            var phoneId = settings.WhatsApp.PhoneNumberId;

            var url = $"https://graph.facebook.com/v19.0/{phoneId}/messages";
            
            // Build the standard WhatsApp payload
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = message.RecipientId,
                type = "text",
                text = new { body = message.Text }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send WhatsApp message. Status: {response.StatusCode}, Details: {errorMsg}");
            }
        }

        private async Task<string> GetWhatsAppMediaUrl(string mediaId, Guid projectId)
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
                if (settings?.WhatsApp == null || string.IsNullOrWhiteSpace(settings.WhatsApp.AccessToken))
                    return string.Empty;

                var accessToken = _encryptionService.Decrypt(settings.WhatsApp.AccessToken);
                var getMediaUrl = $"https://graph.facebook.com/v19.0/{mediaId}";

                using var request = new HttpRequestMessage(HttpMethod.Get, getMediaUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    return doc.RootElement.GetProperty("url").GetString() ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve WhatsApp media URL: {ex.Message}");
            }
            return string.Empty;
        }
    }
}
