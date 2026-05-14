using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using AiChatBox.Api.Services.Tools;

namespace AiChatBox.Api.Services
{
    public class AiChatService(ChatDbContext db,
                                IDbContextFactory<ChatDbContext> dbFactory,
                                LlmProviderFactory llmFactory,
                                AgentService agentService,
                                IChatContextService contextService,
                                IAiLoggingService loggingService,
                                FileProcessingService fileProcessingService,
                                EncryptionService encryptionService,
                                EmbeddingService embeddingService,
                                IHttpContextAccessor httpContextAccessor,
                                ILogger<AiChatService> logger) : IAiChatService
    {
        private readonly ChatDbContext _db = db;
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly LlmProviderFactory _llmFactory = llmFactory;
        private readonly AgentService _agentService = agentService;
        private readonly IChatContextService _contextService = contextService;
        private readonly IAiLoggingService _loggingService = loggingService;
        private readonly FileProcessingService _fileProcessingService = fileProcessingService;
        private readonly EncryptionService _encryption = encryptionService;
        private readonly EmbeddingService _embeddingService = embeddingService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ILogger<AiChatService> _logger = logger;

        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private const int MaxContextMessages = 20;

        private Project? CurrentProject => _httpContextAccessor.HttpContext?.Items["CurrentProject"] as Project;
        private ProjectConfiguration? CurrentConfiguration => _httpContextAccessor.HttpContext?.Items["CurrentConfiguration"] as ProjectConfiguration;
        private ApiKey? CurrentApiKey => _httpContextAccessor.HttpContext?.Items["CurrentApiKey"] as ApiKey;

        public async Task<ChatSession> GetOrCreateSessionAsync(string userId, Guid? sessionId, Guid? projectId = null, Guid? configurationId = null)
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

            var configId = CurrentConfiguration?.Id;
            
            // A/B Testing Logic
            var apiKey = CurrentApiKey;
            if (apiKey != null && apiKey.ExperimentConfigurationId.HasValue && apiKey.ExperimentWeight > 0)
            {
                var dice = Random.Shared.Next(1, 101);
                if (dice <= apiKey.ExperimentWeight)
                {
                    configId = apiKey.ExperimentConfigurationId.Value;
                }
            }

            var session = new ChatSession
            {
                UserId = userId,
                ProjectId = projectId ?? CurrentProject?.Id,
                ConfigurationId = configurationId ?? configId,
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
            var session = await GetOrCreateSessionAsync(userId, request.SessionId, request.ProjectId, request.ConfigurationId);

            // Yield initial session ID
            yield return new ChatStreamChunk { SessionId = session.Id };

            if (request.ToolResult != null)
            {
                var content = JsonSerializer.Serialize(new { toolName = request.ToolResult.ToolName, result = request.ToolResult.Result });
                await SaveMessageAsync(session.Id, "function", content);
            }
            else
            {
                await SaveMessageAsync(session.Id, "user", request.Message ?? "", request.ImageDataUrl, request.AttachedFileId);
            }

            var contextMessages = await _contextService.GetContextMessagesAsync(session.Id, MaxContextMessages);
            
            // Resolve config: Session Pinned Config > Configuration > Request > Project > Default
            var project = CurrentProject;
            var config = request.ConfigurationId.HasValue 
                ? await _db.Configurations.FindAsync(request.ConfigurationId.Value)
                : (session.ConfigurationId.HasValue 
                    ? await _db.Configurations.FindAsync(session.ConfigurationId.Value)
                    : CurrentConfiguration);

            // If we're in the Playground (authenticated via JWT), project/config won't be in HttpContext.Items
            if (project == null && request.ProjectId.HasValue)
            {
                project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value && p.UserId == userId);
            }

            if (config == null && project != null)
            {
                // Find the project's default configuration if not specified
                config = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Name == "Default");
            }

            if (config != null)
            {
                if (config.MaxSpendLimit > 0 && config.CurrentSpend >= config.MaxSpendLimit)
                {
                    yield return new ChatStreamChunk { Error = "Budget limit reached for this project.", Done = true, SessionId = session.Id };
                    yield break;
                }

                if (config.RateLimitRequests > 0)
                {
                    var windowStart = DateTime.UtcNow.AddMinutes(-config.RateLimitWindowMinutes);
                    var requestCount = await _db.AiRequestLogs.CountAsync(l => l.Session.ConfigurationId == config.Id && l.CreatedAt > windowStart);
                    if (requestCount >= config.RateLimitRequests)
                    {
                        yield return new ChatStreamChunk { Error = "Rate limit exceeded. Please try again later.", Done = true, SessionId = session.Id };
                        yield break;
                    }
                }
            }

            var systemPrompt = !string.IsNullOrEmpty(request.SystemPrompt) 
                ? request.SystemPrompt 
                : (!string.IsNullOrEmpty(config?.SystemPrompt) ? config.SystemPrompt 
                : (!string.IsNullOrEmpty(project?.SystemPrompt) ? project.SystemPrompt 
                : await _contextService.BuildSystemPromptAsync(userId)));

            if (request.Context != null)
            {
                var contextStr = $"\n\n[USER CURRENT CONTEXT]\nURL: {request.Context.Url}\nTitle: {request.Context.Title}\nPath: {request.Context.Path}\nUse this information if the user asks questions about 'this page'.";
                systemPrompt += contextStr;
            }

            var modelName = !string.IsNullOrEmpty(request.ModelName)
                ? request.ModelName
                : (config != null ? config.DefaultModel
                : (project != null ? project.ModelName : request.ModelName));

            var provider = !string.IsNullOrEmpty(request.Provider)
                ? request.Provider
                : (config != null ? config.DefaultProvider
                : (project != null ? project.Provider : "gemini"));

            var genericMessages = contextMessages.Select(m => new GenericChatMessage
            {
                Role = m.Role,
                Content = m.Content,
                ImageDataUrl = m.ImageDataUrl,
                AttachedFileId = m.AttachedFileId
            }).ToList();

            var encryptedApiKey = provider.ToLowerInvariant() switch
            {
                "gemini" => config?.GeminiApiKey,
                "groq" => config?.GroqApiKey,
                "grok" => config?.GroqApiKey,
                _ => null
            };

            var apiKeyOverride = _encryption.Decrypt(encryptedApiKey);

            // For RAG retrieval, we always need the Gemini API Key for embeddings
            var geminiApiKeyForRag = provider.ToLowerInvariant() == "gemini" 
                ? apiKeyOverride 
                : _encryption.Decrypt(config?.GeminiApiKey);

            var extraTools = new List<ITool>();
            if (project != null)
            {
                var hasDocs = await _db.KnowledgeDocuments.AnyAsync(d => d.ProjectId == project.Id && d.IsProcessed);
                if (hasDocs)
                {
                    _logger.LogInformation("Registering KnowledgeSearchTool for project {ProjectId}", project.Id);
                    extraTools.Add(new KnowledgeSearchTool(_dbFactory, _embeddingService, project.Id, geminiApiKeyForRag));
                }
            }

            var finalResponseText = new StringBuilder();
            string? errorMessage = null;
            ChatStreamChunk? errorChunk = null;

            async IAsyncEnumerable<ChatStreamChunk> StreamInternal()
            {
                await foreach (var chunk in _agentService.ExecuteAgentAsync(provider, modelName, genericMessages, systemPrompt, userId, project, apiKeyOverride, extraTools, session.Id, cancellationToken))
                {
                    if (chunk.ToolCall != null)
                    {
                        yield return new ChatStreamChunk { ToolCall = chunk.ToolCall, SessionId = session.Id };
                    }
                    else if (!string.IsNullOrEmpty(chunk.Text))
                    {
                        finalResponseText.Append(chunk.Text);
                        yield return new ChatStreamChunk { Text = chunk.Text, SessionId = session.Id };
                    }
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
                else
                {
                    _logger.LogWarning("AI returned an empty response for session {SessionId}, project {ProjectId}, provider {Provider}, model {Model}", 
                        session.Id, project?.Id, provider, modelName);
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
                ProjectId = project?.Id,
                Provider = provider,
                Model = modelName,
                Endpoint = "/api/chat",
                InputTokens = GeminiServerService.StaticEstimateTokenCount(request.Message ?? ""),
                OutputTokens = GeminiServerService.StaticEstimateTokenCount(finalResponseText.ToString()),
                DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                RawRequest = JsonSerializer.Serialize(request, _jsonOptions),
                RawResponse = finalResponseText.ToString(),
                ErrorMessage = errorMessage
            });

            // Update Spend
            if (config != null)
            {
                var inTokens = GeminiServerService.StaticEstimateTokenCount(request.Message ?? "");
                var outTokens = GeminiServerService.StaticEstimateTokenCount(finalResponseText.ToString());
                // Simple cost estimate: $1 per 1M tokens
                var estimatedCost = (inTokens + outTokens) * 0.000001m;
                
                await using var bgDb = await _dbFactory.CreateDbContextAsync();
                var dbConfig = await bgDb.Configurations.FindAsync(config.Id);
                if (dbConfig != null)
                {
                    dbConfig.CurrentSpend += estimatedCost;
                    await bgDb.SaveChangesAsync();
                }
            }

            if (errorChunk != null)
            {
                yield return errorChunk;
            }
        }

        public async Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId)
        {
            var projectId = CurrentProject?.Id;
            return await _db.ChatSessions
                .Where(s => s.UserId == userId && s.ProjectId == projectId && !s.IsArchived)
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
            var projectId = CurrentProject?.Id;
            var sessionExists = await _db.ChatSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.ProjectId == projectId);
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
            var projectId = CurrentProject?.Id;
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.ProjectId == projectId);
            if (session == null) return false;
            session.IsArchived = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ChatSessionDto>> GetArchivedSessionsAsync(string userId)
        {
            var projectId = CurrentProject?.Id;
            return await _db.ChatSessions
                .Where(s => s.UserId == userId && s.ProjectId == projectId && s.IsArchived)
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
            var projectId = CurrentProject?.Id;
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.ProjectId == projectId);
            if (session == null) return false;
            _db.ChatSessions.Remove(session);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
