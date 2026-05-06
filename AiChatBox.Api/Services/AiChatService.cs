using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class AiChatService(ChatDbContext db,
                                IDbContextFactory<ChatDbContext> dbFactory,
                                LlmProviderFactory llmFactory,
                                AgentService agentService,
                                IChatContextService contextService,
                                IAiLoggingService loggingService,
                                FileProcessingService fileProcessingService,
                                ILogger<AiChatService> logger) : IAiChatService
    {
        private readonly ChatDbContext _db = db;
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly LlmProviderFactory _llmFactory = llmFactory;
        private readonly AgentService _agentService = agentService;
        private readonly IChatContextService _contextService = contextService;
        private readonly IAiLoggingService _loggingService = loggingService;
        private readonly FileProcessingService _fileProcessingService = fileProcessingService;
        private readonly ILogger<AiChatService> _logger = logger;

        private const int MaxContextMessages = 20;

        public async Task<ChatSession> GetOrCreateSessionAsync(string userId, Guid? sessionId)
        {
            if (sessionId.HasValue)
            {
                var existing = await _db.ChatSessions
                    .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.UserId == userId);

                if (existing != null)
                {
                    if (existing.IsArchived) 
                    {
                        existing.IsArchived = false;
                        await _db.SaveChangesAsync();
                    }
                    return existing;
                }
            }

            var session = new ChatSession
            {
                UserId = userId,
                Title = "New Chat",
                CreatedAt = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };

            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync();

            return session;
        }

        public async Task<ChatMessage> SaveMessageAsync(Guid sessionId, string role, string content, string? imageDataUrl = null, Guid? attachedFileId = null)
        {
            var message = new ChatMessage
            {
                SessionId = sessionId,
                Role = role,
                Content = content,
                ImageDataUrl = imageDataUrl,
                AttachedFileId = attachedFileId,
                TokenCount = GeminiServerService.StaticEstimateTokenCount(content),
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);

            var session = await _db.ChatSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.LastMessageAt = DateTime.UtcNow;

                if (session.Title == "New Chat" && role == "user" && !string.IsNullOrWhiteSpace(content))
                {
                    session.Title = content.Length > 50 ? content[..50] + "..." : content;
                }
            }

            await _db.SaveChangesAsync();
            return message;
        }

        public async IAsyncEnumerable<ChatStreamChunk> StreamChatAsync(ChatRequest request, string userId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var session = await GetOrCreateSessionAsync(userId, request.SessionId);

            // Yield initial session ID
            yield return new ChatStreamChunk { SessionId = session.Id };

            await SaveMessageAsync(session.Id, "user", request.Message, request.ImageDataUrl, request.AttachedFileId);

            var contextMessages = await _contextService.GetContextMessagesAsync(session.Id, MaxContextMessages);
            var systemPrompt = !string.IsNullOrEmpty(request.SystemPrompt) 
                ? request.SystemPrompt 
                : await _contextService.BuildSystemPromptAsync(userId);

            var genericMessages = contextMessages.Select(m => new GenericChatMessage
            {
                Role = m.Role,
                Content = m.Content,
                ImageDataUrl = m.ImageDataUrl,
                AttachedFileId = m.AttachedFileId
            }).ToList();

            var providerService = _llmFactory.GetProvider(request.Provider);
            var finalResponseText = new StringBuilder();
            string? errorMessage = null;
            ChatStreamChunk? errorChunk = null;

            async IAsyncEnumerable<ChatStreamChunk> StreamInternal()
            {
                await foreach (var chunk in _agentService.ExecuteAgentAsync(request.Provider, request.ModelName, genericMessages, systemPrompt, userId, cancellationToken))
                {
                    finalResponseText.Append(chunk);
                    yield return new ChatStreamChunk { Text = chunk, SessionId = session.Id };
                }

                yield return new ChatStreamChunk { Done = true, SessionId = session.Id };

                var responseText = finalResponseText.ToString();
                if (!string.IsNullOrEmpty(responseText))
                {
                    await using var bgDb = await _dbFactory.CreateDbContextAsync();
                    bgDb.ChatMessages.Add(new ChatMessage
                    {
                        SessionId = session.Id,
                        Role = "model",
                        Content = responseText,
                        TokenCount = GeminiServerService.StaticEstimateTokenCount(responseText),
                        CreatedAt = DateTime.UtcNow
                    });
                    var bgSession = await bgDb.ChatSessions.FindAsync(session.Id);
                    if (bgSession != null) bgSession.LastMessageAt = DateTime.UtcNow;
                    await bgDb.SaveChangesAsync();
                }
            }

            var enumerator = StreamInternal().GetAsyncEnumerator(cancellationToken);
            try
            {
                while (true)
                {
                    ChatStreamChunk chunk;
                    try
                    {
                        if (!await enumerator.MoveNextAsync()) break;
                        chunk = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        errorMessage = ex.Message;
                        errorChunk = new ChatStreamChunk { Error = ex.Message, Done = true, SessionId = session.Id };
                        break;
                    }
                    yield return chunk;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            await _loggingService.LogRequestAsync(new AiRequestLog
            {
                SessionId = session.Id,
                UserId = userId,
                Endpoint = "/api/chat",
                InputTokens = GeminiServerService.StaticEstimateTokenCount(request.Message),
                OutputTokens = GeminiServerService.StaticEstimateTokenCount(finalResponseText.ToString()),
                DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                ErrorMessage = errorMessage
            });

            if (errorChunk != null)
            {
                yield return errorChunk;
            }
        }

        public async Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId)
        {
            return await _db.ChatSessions
                .Where(s => s.UserId == userId && !s.IsArchived)
                .OrderByDescending(s => s.LastMessageAt)
                .Select(s => new ChatSessionDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    CreatedAt = s.CreatedAt,
                    LastMessageAt = s.LastMessageAt,
                    MessageCount = s.Messages.Count
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, string userId)
        {
            var sessionExists = await _db.ChatSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId);
            if (!sessionExists) return [];

            return await _db.ChatMessages
                .Include(m => m.AttachedFile)
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    ImageDataUrl = m.ImageDataUrl,
                    AttachedFileId = m.AttachedFileId,
                    AttachedFileName = m.AttachedFile != null ? m.AttachedFile.OriginalFileName : null,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> ArchiveSessionAsync(Guid sessionId, string userId)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
            if (session == null) return false;
            session.IsArchived = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ChatSessionDto>> GetArchivedSessionsAsync(string userId)
        {
            return await _db.ChatSessions
                .Where(s => s.UserId == userId && s.IsArchived)
                .OrderByDescending(s => s.LastMessageAt)
                .Select(s => new ChatSessionDto 
                { 
                    Id = s.Id, 
                    Title = s.Title, 
                    CreatedAt = s.CreatedAt, 
                    LastMessageAt = s.LastMessageAt, 
                    MessageCount = s.Messages.Count 
                })
                .ToListAsync();
        }

        public async Task<bool> HardDeleteSessionAsync(Guid sessionId, string userId)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
            if (session == null) return false;
            _db.ChatSessions.Remove(session);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
