using Microsoft.AspNetCore.SignalR;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class LiveAudioHub(ILogger<LiveAudioHub> logger, 
                            LiveSessionManager sessionManager, 
                            IHubContext<LiveAudioHub> hubContext,
                            ApiKeyService apiKeyService,
                            EncryptionService encryptionService) : Hub
    {
        private readonly ILogger<LiveAudioHub> _logger = logger;
        private readonly LiveSessionManager _sessionManager = sessionManager;
        private readonly IHubContext<LiveAudioHub> _hubContext = hubContext;
        private readonly ApiKeyService _apiKeyService = apiKeyService;
        private readonly EncryptionService _encryption = encryptionService;

        /// <summary>
        /// Start a live session using an API Key (widget / end-user integration).
        /// </summary>
        public async Task StartLive(string userId, string? voiceName = null, string apiKey = "")
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

                var systemPrompt = config?.SystemPrompt ?? project.SystemPrompt;
                var geminiApiKeyOverride = _encryption.Decrypt(config?.GeminiApiKey);

                await StartSessionInternal(connectionId, userId, voiceName, systemPrompt, project.Id, geminiApiKeyOverride);
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
        public async Task StartLiveDashboard(string userId, string? voiceName = null, string? projectId = null, string? configurationId = null)
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

                var systemPrompt = config?.SystemPrompt ?? project.SystemPrompt;
                var geminiApiKeyOverride = _encryption.Decrypt(config?.GeminiApiKey);

                await StartSessionInternal(connectionId, userId, voiceName, systemPrompt, project.Id, geminiApiKeyOverride);
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
        private async Task StartSessionInternal(string connectionId, string userId, string? voiceName, string? systemPrompt, Guid? projectId, string? geminiApiKeyOverride)
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
                async (id, name, args) =>
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveToolCall", id, name, args);
                },
                projectId,
                geminiApiKeyOverride
            );

            var session = _sessionManager.GetSession(connectionId);
            session?.OnError += async (error) =>
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveError", error);
                };
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
                if (session != null)
                {
                    await session.SendTextMessageAsync(text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendText hub method for {ConnectionId}", Context.ConnectionId);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _sessionManager.StopSessionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
