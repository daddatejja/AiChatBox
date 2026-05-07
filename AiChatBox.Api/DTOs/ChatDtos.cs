namespace AiChatBox.Api.DTOs
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public Guid? SessionId { get; set; }
        public string? ImageDataUrl { get; set; }
        public Guid? AttachedFileId { get; set; }
        public string Provider { get; set; } = "gemini";
        public string? ModelName { get; set; }
        public string? SystemPrompt { get; set; }
    }

    public class TranscribeRequest
    {
        public string AudioBase64 { get; set; } = string.Empty;
        public string Language { get; set; } = "auto";
    }

    public class TtsRequest
    {
        public string Text { get; set; } = string.Empty;
        public string Voice { get; set; } = "en-US-Standard-A";
    }

    public class GenericChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageDataUrl { get; set; }
        public Guid? AttachedFileId { get; set; }
    }

    public class ChatSessionDto
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
        public int MessageCount { get; set; }
    }

    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageDataUrl { get; set; }
        public Guid? AttachedFileId { get; set; }
        public string? AttachedFileName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChatStreamChunk
    {
        public string? Text { get; set; }
        public ToolCallDto? ToolCall { get; set; }
        public Guid? SessionId { get; set; }
        public bool Done { get; set; }
        public string? Error { get; set; }
        public ReportDownloadDto? ReportInfo { get; set; }
    }

    public class ToolCallDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    public class AgentChunk
    {
        public string? Text { get; set; }
        public ToolCallDto? ToolCall { get; set; }
    }

    public class ReportDownloadDto
    {
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
