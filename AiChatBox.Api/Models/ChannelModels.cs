using System;

namespace AiChatBox.Api.Models
{
    public class InboundMessage
    {
        public string SenderId { get; set; } = string.Empty;         // External User Chat ID or Phone Number
        public string Text { get; set; } = string.Empty;             // Content of the message
        public string Channel { get; set; } = string.Empty;          // "whatsapp", "slack", "telegram"
        public string? AttachmentUrl { get; set; }                   // Optional incoming file attachment URL
        public string? SessionExternalId { get; set; }               // External session identifier (e.g. Slack thread ID)
        public Guid ProjectId { get; set; }                          // Target Project ID
        public string? SenderName { get; set; }                      // Optional parsed sender display name
    }

    public class OutboundMessage
    {
        public string RecipientId { get; set; } = string.Empty;       // External Recipient ID or Phone Number
        public string Text { get; set; } = string.Empty;              // Response markdown/text
        public string Channel { get; set; } = string.Empty;           // "whatsapp", "slack", "telegram"
        public Guid SessionId { get; set; }                          // Internal ChatSession ID
        public Guid ProjectId { get; set; }                          // Internal Project ID
    }
    
    public class ChannelSettings
    {
        public WhatsAppSettings? WhatsApp { get; set; }
        public SlackSettings? Slack { get; set; }
        public TelegramSettings? Telegram { get; set; }
        public TeamsSettings? Teams { get; set; }
        public OpenWaSettings? OpenWa { get; set; }
    }

    public class OpenWaSettings
    {
        public string InstanceUrl { get; set; } = string.Empty;
        public string SessionName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public class WhatsAppSettings
    {
        public string PhoneNumberId { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string VerifyToken { get; set; } = string.Empty;
        public string AppSecret { get; set; } = string.Empty;
    }

    public class SlackSettings
    {
        public string BotToken { get; set; } = string.Empty;
        public string SigningSecret { get; set; } = string.Empty;
    }

    public class TelegramSettings
    {
        public string BotToken { get; set; } = string.Empty;
        public string SecretToken { get; set; } = string.Empty;
    }

    public class TeamsSettings
    {
        public string AppId { get; set; } = string.Empty;
        public string AppPassword { get; set; } = string.Empty;
    }
}
