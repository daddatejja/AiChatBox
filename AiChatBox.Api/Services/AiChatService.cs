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
using Microsoft.AspNetCore.SignalR;

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
                                RuleEngine ruleEngine,
                                HandoffService handoffService,
                                FlowExecutionService flowService,
                                IHubContext<LiveChatHub> chatHubContext,
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
        private readonly RuleEngine _ruleEngine = ruleEngine;
        private readonly HandoffService _handoffService = handoffService;
        private readonly FlowExecutionService _flowService = flowService;
        private readonly IHubContext<LiveChatHub> _chatHubContext = chatHubContext;
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

            async IAsyncEnumerable<ChatStreamChunk> StreamFlowAsync(ChatSession s, string msg, Project p)
            {
                var flowStream = _flowService.ExecuteFlowStepAsync(s, msg, p);
                string flowFullText = "";
                await foreach (var chunk in flowStream)
                {
                    if (chunk.Text != null)
                    {
                        flowFullText += chunk.Text;
                    }
                    if (chunk.RuleResponse != null)
                    {
                        var rType = chunk.RuleResponse.ResponseType ?? "text";
                        flowFullText += $" [{rType.ToUpperInvariant()} RESPONSE] {chunk.RuleResponse.Payload}";
                    }
                    
                    chunk.SessionId = s.Id;
                    yield return chunk;
                }

                var flowSavedMsg = await SaveMessageAsync(s.Id, "model", flowFullText);
                yield return new ChatStreamChunk { Done = true, SessionId = s.Id, MessageId = flowSavedMsg.Id };
            }

            // Yield initial session ID
            yield return new ChatStreamChunk { SessionId = session.Id };

            if (request.ToolResults != null && request.ToolResults.Count > 0)
            {
                foreach (var toolRes in request.ToolResults)
                {
                    var content = JsonSerializer.Serialize(new { toolCallId = toolRes.ToolCallId, toolName = toolRes.ToolName, result = toolRes.Result, thoughtSignature = toolRes.ThoughtSignature });
                    await SaveMessageAsync(session.Id, "function", content);
                }
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
                project = await _db.Projects
                    .Include(p => p.Database)
                    .FirstOrDefaultAsync(p => p.Id == request.ProjectId.Value && p.UserId == userId);
            }

            if (config == null && project != null)
            {
                // Find the project's default configuration if not specified
                config = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Name == "Default");
                if (config == null)
                {
                    config = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == project.Id);
                }
            }

            if (config != null)
            {
                session.Configuration = config;
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

            systemPrompt += "\n\n[CITATION DIRECTIVE]\nWhen answering questions based on the knowledge base search results, you must explicitly cite the source filenames (e.g., '[filename.pdf]' or 'according to filename.pdf') provided in the search result headers to credit the original document.";

            if (request.Context != null)
            {
                var contextStr = $"\n\n[USER CURRENT CONTEXT]\nURL: {request.Context.Url}\nTitle: {request.Context.Title}\nPath: {request.Context.Path}\nUse this information if the user asks questions about 'this page'.";
                systemPrompt += contextStr;
            }

            // ─── Template Variable Substitution ───
            // Replace {{variable}} placeholders with configured values
            if (config?.PromptTemplateVariablesJson != null)
            {
                try
                {
                    var vars = JsonSerializer.Deserialize<Dictionary<string, string>>(config.PromptTemplateVariablesJson);
                    if (vars != null)
                    {
                        foreach (var (key, value) in vars)
                            systemPrompt = systemPrompt.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch { }
            }
            // Built-in runtime variables (always available)
            systemPrompt = systemPrompt
                .Replace("{{date}}", DateTime.UtcNow.ToString("yyyy-MM-dd"))
                .Replace("{{time}}", DateTime.UtcNow.ToString("HH:mm UTC"));

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

            var apiKeyOverride = _llmFactory.ResolveApiKey(provider, config);

            // ─── Human Handoff Check ───
            // If session is currently being handled by a human agent, don't call LLM
            if (session.HandoffStatus == "active")
            {
                // Message was already saved above — notify the agent via SignalR
                if (!string.IsNullOrEmpty(request.Message))
                {
                    var lastMsg = session.Messages?.OrderByDescending(m => m.CreatedAt).FirstOrDefault(m => m.Role == "user") 
                        ?? await _db.ChatMessages.OrderByDescending(m => m.CreatedAt).FirstOrDefaultAsync(m => m.SessionId == session.Id && m.Role == "user");

                    if (lastMsg != null)
                    {
                        await _chatHubContext.Clients.Group($"session-{session.Id}").SendAsync("ReceiveUserMessage", new
                        {
                            id = lastMsg.Id,
                            sessionId = session.Id,
                            content = lastMsg.Content,
                            createdAt = lastMsg.CreatedAt,
                            role = "user"
                        });
                    }
                }

                // yield an empty chunk to satisfy the stream response without hallucinating
                yield return new ChatStreamChunk { Text = "", Done = true, SessionId = session.Id };
                yield break;
            }

            // ─── Flow Engine Check ───
            // Check if we are ALREADY in a flow
            if (session.ActiveFlowId != null)
            {
                await foreach (var chunk in StreamFlowAsync(session, request.Message ?? "", project))
                {
                    yield return chunk;
                }
                yield break;
            }

            // ─── Rule-Based Response Engine ───
            // Check rules BEFORE any LLM call. If a rule matches and ResponseType is not "ai",
            // we respond instantly — zero LLM cost. For "ai" type, we inject a custom prompt
            // and continue to the LLM as normal.
            if (project != null && request.ToolResults == null && !string.IsNullOrEmpty(request.Message))
            {
                var ruleResult = await _ruleEngine.TryMatchAsync(project.Id, request.Message, config,
                    contextMessages.Where(m => m.Role == "user").Select(m => m.Content).TakeLast(3).ToList(),
                    cancellationToken);
                if (ruleResult != null)
                {
                    var rType = (ruleResult.ResponseType ?? "text").ToLowerInvariant();

                    _logger.LogInformation("Rule matched ({MatchType}/{ResponseType}, confidence={Confidence:F2}) for project {ProjectId}",
                        ruleResult.MatchType, rType, ruleResult.Confidence, project.Id);

                    if (rType == "ai")
                    {
                        // Inject additional prompt instructions and fall through to LLM
                        if (!string.IsNullOrWhiteSpace(ruleResult.ResponsePayload))
                            systemPrompt = systemPrompt + "\n\n[RULE CONTEXT]\n" + ruleResult.ResponsePayload;
                        // Do NOT yield break — fall through to LLM below
                    }
                    else if (rType == "flow")
                    {
                        if (Guid.TryParse(ruleResult.ResponsePayload, out Guid flowId))
                        {
                            var triggered = await _flowService.TriggerFlowByIdAsync(session, flowId);
                            if (triggered)
                            {
                                await foreach (var chunk in StreamFlowAsync(session, request.Message ?? "", project))
                                {
                                    yield return chunk;
                                }
                                yield break;
                            }
                        }
                    }
                    else
                    {
                        // All other types short-circuit the LLM
                        string savedContent = rType == "text"
                            ? ruleResult.Response
                            : $"[{rType.ToUpperInvariant()} RESPONSE] {ruleResult.ResponsePayload}";

                        var ruleMsg = await SaveMessageAsync(session.Id, "model", savedContent);

                        if (rType == "text")
                        {
                            yield return new ChatStreamChunk { Text = ruleResult.Response, SessionId = session.Id };
                        }
                        else
                        {
                            // Send a rich response chunk; widget handles rendering
                            yield return new ChatStreamChunk
                            {
                                SessionId = session.Id,
                                RuleResponse = new DTOs.RuleResponseChunk
                                {
                                    ResponseType = rType,
                                    Payload = ruleResult.ResponsePayload
                                }
                            };

                            // Also stream any text (e.g. card body or redirect message) for non-widget clients
                            if (!string.IsNullOrWhiteSpace(ruleResult.Response))
                                yield return new ChatStreamChunk { Text = ruleResult.Response, SessionId = session.Id };
                        }

                        // Log as a zero-cost request
                        await _loggingService.LogRequestAsync(new Models.AiRequestLog
                        {
                            SessionId = session.Id,
                            Provider = "rules",
                            Model = ruleResult.MatchType == "intent" ? "intent-classifier" : "rule-engine",
                            InputTokens = 0,
                            OutputTokens = 0,
                            DurationMs = 1,
                            CreatedAt = DateTime.UtcNow
                        });

                        yield return new ChatStreamChunk { Done = true, SessionId = session.Id, MessageId = ruleMsg.Id };
                        yield break;
                    }
                }
            }

            // Check if this is the very first message/start of session and trigger onStart flow if configured
            var msgCount = await _db.ChatMessages.CountAsync(m => m.SessionId == session.Id);
            if (msgCount <= 1)
            {
                if (await _flowService.TryTriggerOnStartFlowAsync(session))
                {
                    await foreach (var chunk in StreamFlowAsync(session, request.Message ?? "", project))
                    {
                        yield return chunk;
                    }
                    yield break;
                }
            }

            // Check if we should TRIGGER a flow
            if (await _flowService.TryTriggerFlowAsync(session, request.Message ?? ""))
            {
                await foreach (var chunk in StreamFlowAsync(session, request.Message ?? "", project))
                {
                    yield return chunk;
                }
                yield break;
            }

            // If session is queued (waiting for agent), show queue message
            if (session.HandoffStatus == "queued")
            {
                var queueMsg = config?.HandoffQueueMessage ?? "You're in the queue for a live agent. Please hold on.";
                var qMsg = await SaveMessageAsync(session.Id, "model", queueMsg);
                yield return new ChatStreamChunk { Text = queueMsg, SessionId = session.Id };
                yield return new ChatStreamChunk { Done = true, SessionId = session.Id, MessageId = qMsg.Id };
                yield break;
            }

            // Check if message triggers handoff (keyword + intent classification)
            if (config != null && config.HandoffEnabled && request.ToolResults == null && !string.IsNullOrEmpty(request.Message))
            {
                var recentUserMessages = contextMessages
                    .Where(m => m.Role == "user")
                    .Select(m => m.Content)
                    .TakeLast(5)
                    .ToList();

                var handoffResult = await _handoffService.ShouldTriggerHandoffAsync(
                    request.Message, config, recentUserMessages, cancellationToken);

                if (handoffResult.ShouldEscalate)
                {
                    _logger.LogInformation("Handoff triggered for session {SessionId} ({MatchType}, confidence={Confidence:F2})",
                        session.Id, handoffResult.MatchType, handoffResult.Confidence);
                    await _handoffService.QueueSessionAsync(session.Id);

                    var queueMsg = config.HandoffQueueMessage ?? "I'm connecting you with a live agent. Please hold on — someone will be with you shortly.";
                    var hMsg = await SaveMessageAsync(session.Id, "model", queueMsg);
                    yield return new ChatStreamChunk { Text = queueMsg, SessionId = session.Id };
                    yield return new ChatStreamChunk { Done = true, SessionId = session.Id, MessageId = hMsg.Id };
                    yield break;
                }
            }

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
                    if (chunk.ToolCalls != null && chunk.ToolCalls.Count > 0)
                    {
                        await SaveMessageAsync(session.Id, "model", JsonSerializer.Serialize(new { toolCalls = chunk.ToolCalls }, _jsonOptions));
                        yield return new ChatStreamChunk { ToolCalls = chunk.ToolCalls, SessionId = session.Id };
                    }
                    else if (chunk.ToolResult != null)
                    {
                        await SaveMessageAsync(session.Id, "function", JsonSerializer.Serialize(new { toolName = chunk.ToolResult.ToolName, result = chunk.ToolResult.Result, thoughtSignature = chunk.ToolResult.ThoughtSignature }, _jsonOptions));
                        yield return new ChatStreamChunk { ToolResult = chunk.ToolResult, SessionId = session.Id };
                    }
                    else if (!string.IsNullOrEmpty(chunk.Text))
                    {
                        finalResponseText.Append(chunk.Text);
                        yield return new ChatStreamChunk { Text = chunk.Text, SessionId = session.Id };
                    }
                }

                var responseText = finalResponseText.ToString();
                Guid? savedMessageId = null;
                if (!string.IsNullOrEmpty(responseText))
                {
                    await using var bgDb = await _dbFactory.CreateDbContextAsync();
                    var msg = new ChatMessage
                    {
                        SessionId = session.Id,
                        Role = "model",
                        Content = responseText,
                        TokenCount = GeminiServerService.StaticEstimateTokenCount(responseText),
                        CreatedAt = DateTime.UtcNow
                    };
                    bgDb.ChatMessages.Add(msg);
                    var bgSession = await bgDb.ChatSessions.FindAsync(session.Id);
                    if (bgSession != null) bgSession.LastMessageAt = DateTime.UtcNow;
                    await bgDb.SaveChangesAsync();
                    savedMessageId = msg.Id;
                }
                else
                {
                    _logger.LogWarning("AI returned an empty response for session {SessionId}, project {ProjectId}, provider {Provider}, model {Model}", 
                        session.Id, project?.Id, provider, modelName);
                }

                yield return new ChatStreamChunk { Done = true, SessionId = session.Id, MessageId = savedMessageId };
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

        public async Task<IEnumerable<ChatSessionDto>> GetSessionsAsync(string userId, Guid? projectId = null)
        {
            var pId = projectId ?? CurrentProject?.Id;
            return await _db.ChatSessions
                .Where(s => s.UserId == userId && s.ProjectId == pId && !s.IsArchived)
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

        public async Task<IEnumerable<ChatMessageDto>> GetSessionMessagesAsync(Guid sessionId, string userId, Guid? projectId = null)
        {
            var pId = projectId ?? CurrentProject?.Id;
            var sessionExists = await _db.ChatSessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.ProjectId == pId);
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

        public async Task<bool> ArchiveSessionAsync(Guid sessionId, string userId, Guid? projectId = null)
        {
            var pId = projectId ?? CurrentProject?.Id;
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.ProjectId == pId);
            if (session == null) return false;
            session.IsArchived = true;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ChatSessionDto>> GetArchivedSessionsAsync(string userId, Guid? projectId = null)
        {
            var pId = projectId ?? CurrentProject?.Id;
            return await _db.ChatSessions
                .Where(s => s.UserId == userId && s.ProjectId == pId && s.IsArchived)
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

        public async Task<bool> HardDeleteSessionAsync(Guid sessionId, string userId, Guid? projectId = null)
        {
            var pId = projectId ?? CurrentProject?.Id;
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.ProjectId == pId);
            if (session == null) return false;
            _db.ChatSessions.Remove(session);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
