using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Interfaces
{
    public interface IAiChatService
    {
        Task<ChatSession> GetOrCreateSessionAsync(string userId, Guid? sessionId, Guid? projectId = null, Guid? configurationId = null, string sessionType = "text", Guid? parentSessionId = null);
        Task<ChatMessage> SaveMessageAsync(Guid sessionId, string role, string content, string? imageDataUrl = null, Guid? attachedFileId = null);
        IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(ChatRequest request, string userId, CancellationToken cancellationToken);
        
        Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId, Guid? projectId = null);
        Task<IEnumerable<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, string userId, Guid? projectId = null);
        Task<bool> ArchiveSessionAsync(Guid sessionId, string userId, Guid? projectId = null);
        Task<IEnumerable<ChatSessionDto>> GetArchivedSessionsAsync(string userId, Guid? projectId = null);
        Task<bool> HardDeleteSessionAsync(Guid sessionId, string userId, Guid? projectId = null);
    }
}
