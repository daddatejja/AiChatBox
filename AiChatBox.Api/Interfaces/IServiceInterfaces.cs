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

    public interface IGeminiLiveService : IAsyncDisposable
    {
        Guid? ProjectId { get; set; }
        event Func<byte[], Task>? OnAudioReceived;
        event Func<string, bool, Task>? OnTextReceived;
        event Func<string, Task>? OnInputTranscribed;
        event Func<string, string, Dictionary<string, object>, Task>? OnToolCall;
        event Action<string>? OnError;

        Task ConnectAsync(string userId, string? voiceName = null, string? systemPrompt = null, CancellationToken cancellationToken = default);
        Task SendAudioChunkAsync(string base64Data, CancellationToken cancellationToken = default);
        Task SendTextMessageAsync(string text, CancellationToken cancellationToken = default);
        Task CompleteTurnAsync(CancellationToken cancellationToken = default);
    }
}
