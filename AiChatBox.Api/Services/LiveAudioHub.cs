using Microsoft.AspNetCore.SignalR;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class LiveAudioHub(ILogger<LiveAudioHub> logger, 
                            LiveSessionManager sessionManager, 
                            IHubContext<LiveAudioHub> hubContext,
                            ApiKeyService apiKeyService,
                            EncryptionService encryptionService,
                            AiChatBox.Api.Data.ChatDbContext db,
                            RuleEngine ruleEngine,
                            HandoffService handoffService,
                            FlowExecutionService flowExecutionService) : Hub
    {
        private readonly ILogger<LiveAudioHub> _logger = logger;
        private readonly LiveSessionManager _sessionManager = sessionManager;
        private readonly IHubContext<LiveAudioHub> _hubContext = hubContext;
        private readonly ApiKeyService _apiKeyService = apiKeyService;
        private readonly EncryptionService _encryption = encryptionService;
        private readonly AiChatBox.Api.Data.ChatDbContext _db = db;
        private readonly RuleEngine _ruleEngine = ruleEngine;
        private readonly HandoffService _handoffService = handoffService;
        private readonly FlowExecutionService _flowExecutionService = flowExecutionService;

        /// <summary>
        /// Start a live session using an API Key (widget / end-user integration).
        /// </summary>
        public async Task StartLive(string userId, string? voiceName = null, string apiKey = "", string? sessionId = null)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Widget client {ConnectionId} starting live session for user {UserId}", connectionId, userId);

            if (string.IsNullOrEmpty(apiKey))
            {
                await Clients.Caller.SendAsync("ReceiveError", "Unauthorized: API Key required.");
                return;
            }

            try
            {
                var origin = Context.GetHttpContext()?.Request.Headers.Origin.ToString();
                var (project, config, _) = await _apiKeyService.ValidateApiKeyAsync(apiKey, origin);
                
                if (project == null)
                {
                    _logger.LogWarning("Unauthorized live session attempt with API Key from origin: {Origin}", origin);
                    await Clients.Caller.SendAsync("ReceiveError", "Unauthorized: Invalid API Key or Origin.");
                    return;
                }

                var isLiveVoiceEnabled = config?.LiveVoiceEnabled ?? false;
                if (!isLiveVoiceEnabled)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Live voice is not enabled for this configuration.");
                    return;
                }

                var systemPrompt = config?.SystemPrompt ?? project.SystemPrompt;
                var geminiApiKeyOverride = _encryption.Decrypt(config?.GeminiApiKey);

                Guid? sessId = Guid.NewGuid();
                Guid? parentSessId = !string.IsNullOrEmpty(sessionId) && Guid.TryParse(sessionId, out var psid) ? psid : (Guid?)null;
                await StartSessionInternal(connectionId, userId, voiceName, systemPrompt, project.Id, config?.Id, geminiApiKeyOverride, sessId, parentSessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start live session for widget client {ConnectionId}", connectionId);
                await Clients.Caller.SendAsync("ReceiveError", "Failed to connect to Gemini.");
            }
        }

        /// <summary>
        /// Start a live session using JWT authentication (Dashboard Playground).
        /// ProjectId and ConfigurationId are strings parsed internally.
        /// </summary>
        public async Task StartLiveDashboard(string userId, string? voiceName = null, string? projectId = null, string? configurationId = null, string? sessionId = null)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Dashboard client {ConnectionId} starting live session for user {UserId}", connectionId, userId);

            if (Context.User?.Identity?.IsAuthenticated != true)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Unauthorized: Authentication required.");
                return;
            }

            if (string.IsNullOrEmpty(projectId) || !Guid.TryParse(projectId, out var parsedProjectId))
            {
                await Clients.Caller.SendAsync("ReceiveError", "Invalid or missing Project ID.");
                return;
            }

            try
            {
                var authUserId = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var project = await _apiKeyService.GetProjectByIdAsync(parsedProjectId, authUserId);

                if (project == null)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Unauthorized: Project not found or access denied.");
                    return;
                }

                ProjectConfiguration? config = null;
                if (!string.IsNullOrEmpty(configurationId) && Guid.TryParse(configurationId, out var parsedConfigId))
                {
                    config = await _apiKeyService.GetConfigurationByIdAsync(parsedConfigId, parsedProjectId);
                }
                
                config ??= await _apiKeyService.GetDefaultConfigurationAsync(parsedProjectId);

                var isLiveVoiceEnabled = config?.LiveVoiceEnabled ?? false;
                if (!isLiveVoiceEnabled)
                {
                    await Clients.Caller.SendAsync("ReceiveError", "Live voice is not enabled for this configuration.");
                    return;
                }

                var systemPrompt = config?.SystemPrompt ?? project.SystemPrompt;
                var geminiApiKeyOverride = _encryption.Decrypt(config?.GeminiApiKey);

                Guid? sessId = Guid.NewGuid(); // Always generate a fresh unique session ID for isolated live session
                Guid? parentSessId = !string.IsNullOrEmpty(sessionId) && Guid.TryParse(sessionId, out var psid) ? psid : (Guid?)null;
                await StartSessionInternal(connectionId, userId, voiceName, systemPrompt, project.Id, config?.Id, geminiApiKeyOverride, sessId, parentSessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start live session for dashboard client {ConnectionId}", connectionId);
                await Clients.Caller.SendAsync("ReceiveError", "Failed to connect to Gemini.");
            }
        }

        /// <summary>
        /// Shared session initialization logic used by both StartLive and StartLiveDashboard.
        /// </summary>
        private async Task StartSessionInternal(string connectionId, string userId, string? voiceName, string? systemPrompt, Guid? projectId, Guid? configurationId, string? geminiApiKeyOverride, Guid? sessionId, Guid? parentSessionId = null)
        {
            await _sessionManager.StartSessionAsync(connectionId, userId, voiceName, systemPrompt,
                async (pcmData) => 
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveAudioChunk", pcmData);
                },
                async (text, isThought) => 
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTextChunk", text, isThought);
                },
                async (text) => 
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveInputTranscription", text);
                },
                async (id, name, args, isBackend) =>
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveToolCall", id, name, args, isBackend);
                },
                async (id, name, result) =>
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveToolResult", id, name, result);
                },
                projectId,
                configurationId,
                geminiApiKeyOverride,
                sessionId,
                parentSessionId
            );

            var session = _sessionManager.GetSession(connectionId);
            session?.OnError += async (error) =>
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveError", error);
                };
            if (session != null)
            {
                session.OnDisconnected += async (reason) =>
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveDisconnected", reason);
                };
            }
        }

        public async Task SendAudio(string data)
        {
            try
            {
                var session = _sessionManager.GetSession(Context.ConnectionId);
                if (session != null)
                {
                    await session.SendAudioChunkAsync(data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendAudio hub method for {ConnectionId}", Context.ConnectionId);
            }
        }

        public async Task SendText(string text)
        {
            try
            {
                var session = _sessionManager.GetSession(Context.ConnectionId);
                if (session != null && session.SessionId.HasValue && session.ProjectId.HasValue)
                {
                    var chatSession = await _db.ChatSessions.FindAsync(session.SessionId.Value);
                    var config = session.ConfigurationId.HasValue 
                        ? await _db.Configurations.FindAsync(session.ConfigurationId.Value) 
                        : null;

                    if (chatSession != null)
                    {
                        // 1. Check Handoff
                        var handoffCheck = await _handoffService.ShouldTriggerHandoffAsync(text, config);
                        if (handoffCheck.ShouldEscalate || chatSession.HandoffStatus is "queued" or "active")
                        {
                            if (chatSession.HandoffStatus == "ai")
                            {
                                await _handoffService.QueueSessionAsync(chatSession.Id);
                            }
                            await Clients.Caller.SendAsync("ReceiveTextChunk", "Support agent has been requested. Please switch to text chat.", false);
                            return;
                        }

                        // 2. Check Flow
                        if (await _flowExecutionService.TryTriggerFlowAsync(chatSession, text))
                        {
                            await Clients.Caller.SendAsync("ReceiveTextChunk", "A conversational flow was triggered. Please switch to text chat to continue.", false);
                            return;
                        }

                        // 3. Check Rule
                        var ruleMatch = await _ruleEngine.TryMatchAsync(session.ProjectId.Value, text, config);
                        if (ruleMatch != null)
                        {
                            if (ruleMatch.ResponseType == "text")
                            {
                                await Clients.Caller.SendAsync("ReceiveTextChunk", ruleMatch.Response, false);
                            }
                            else
                            {
                                // Option 1: Fallback for rich responses
                                await Clients.Caller.SendAsync("ReceiveTextChunk", "Please switch to text chat to view this content.", false);
                            }
                            return;
                        }
                    }

                    // Forward to Gemini if no rules/flows/handoff triggered
                    await session.SendTextMessageAsync(text);
                }
                else if (session != null)
                {
                    await session.SendTextMessageAsync(text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendText hub method for {ConnectionId}", Context.ConnectionId);
            }
        }

        public async Task SendToolResult(string callId, string result)
        {
            try
            {
                var session = _sessionManager.GetSession(Context.ConnectionId);
                if (session != null)
                {
                    await session.SendToolResponseAsync(callId, "client_tool", new { result }, default);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendToolResult hub method for {ConnectionId}", Context.ConnectionId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _sessionManager.StopSessionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
