using AiChatBox.Api.Models;

namespace AiChatBox.Api.Interfaces
{
    public interface IChatContextService
    {
        Task<IList<ChatMessage>> GetContextMessagesAsync(Guid sessionId, int maxMessages = 20, int maxTokens = 4000);
        Task<string> BuildSystemPromptAsync(string userId);
    }

    public interface IAiLoggingService
    {
        Task LogRequestAsync(AiRequestLog log);
    }

    public interface IFileService
    {
        Task<UploadedFile> SaveFileAsync(IFormFile file, string userId);
        Task<UploadedFile?> GetFileAsync(Guid fileId, string userId);
    }

    public interface IAiAudioService
    {
        Task<string> TranscribeAsync(byte[] audioData, string language = "auto");
        Task<byte[]> TextToSpeechAsync(string text, string voice = "en-US-Standard-A");
    }

    public interface IGeminiLiveService
    {
        Task StartSessionAsync(string connectionId, string userId, string model = "gemini-2.0-flash-exp");
        Task StopSessionAsync(string connectionId);
        Task SendAudioChunkAsync(string connectionId, byte[] audioData);
        Task SendTextMessageAsync(string connectionId, string text);
    }
}
