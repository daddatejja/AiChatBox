using AiChatBox.Api.Models;
using Pgvector;

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

    public interface IEmbeddingService
    {
        Task<Vector> GetEmbeddingAsync(string text, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null);
        Task<List<Vector>> GetBatchEmbeddingsAsync(List<string> texts, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null);
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
        Guid? ConfigurationId { get; set; }
        Guid? SessionId { get; set; }
        string? UserId { get; set; }
        string? ApiKeyOverride { get; set; }
        event Func<byte[], Task>? OnAudioReceived;
        event Func<string, bool, Task>? OnTextReceived;
        event Func<string, Task>? OnInputTranscribed;
        event Func<string, string, Dictionary<string, object>, bool, Task>? OnToolCall;
        event Func<string, string, object, Task>? OnToolResult;
        event Action<string>? OnError;
        event Action<string>? OnDisconnected;

        Task ConnectAsync(string userId, string? voiceName = null, string? systemPrompt = null, CancellationToken cancellationToken = default);
        Task SendAudioChunkAsync(string base64Data, CancellationToken cancellationToken = default);
        Task SendTextMessageAsync(string text, CancellationToken cancellationToken = default);
        Task CompleteTurnAsync(CancellationToken cancellationToken = default);
        Task SendToolResponseAsync(string id, string name, object response, CancellationToken cancellationToken = default);
    }
}
