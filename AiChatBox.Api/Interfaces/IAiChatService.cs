using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Interfaces
{
    public interface IAiChatService
    {
        Task<ChatSession> GetOrCreateSessionAsync(string userId, Guid? sessionId, Guid? projectId = null, Guid? configurationId = null);
        Task<ChatMessage> SaveMessageAsync(Guid sessionId, string role, string content, string? imageDataUrl = null, Guid? attachedFileId = null);
        IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(ChatRequest request, string userId, CancellationToken cancellationToken);
        
        Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId);
        Task<IEnumerable<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, string userId);
        Task<bool> ArchiveSessionAsync(Guid sessionId, string userId);
        Task<IEnumerable<ChatSessionDto>> GetArchivedSessionsAsync(string userId);
        Task<bool> HardDeleteSessionAsync(Guid sessionId, string userId);
    }
}
