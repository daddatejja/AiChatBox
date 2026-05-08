using Microsoft.AspNetCore.SignalR;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class LiveAudioHub(ILogger<LiveAudioHub> logger, 
                            LiveSessionManager sessionManager, 
                            IHubContext<LiveAudioHub> hubContext,
                            ApiKeyService apiKeyService) : Hub
    {
        private readonly ILogger<LiveAudioHub> _logger = logger;
        private readonly LiveSessionManager _sessionManager = sessionManager;
        private readonly IHubContext<LiveAudioHub> _hubContext = hubContext;
        private readonly ApiKeyService _apiKeyService = apiKeyService;

        public async Task StartLive(string userId, string? voiceName = null, string? apiKey = null, string? systemPrompt = null)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} starting live session for user {UserId}", connectionId, userId);

            try
            {
                // Resolve project and system prompt
                Project? project = null;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    var origin = Context.GetHttpContext()?.Request.Headers.Origin.ToString();
                    ProjectConfiguration? config;
                    (project, config, _) = await _apiKeyService.ValidateApiKeyAsync(apiKey, origin);
                    
                    if (project == null)
                    {
                        _logger.LogWarning("Unauthorized live session attempt with API Key from origin: {Origin}", origin);
                        await Clients.Caller.SendAsync("ReceiveError", "Unauthorized: Invalid API Key or Origin.");
                        return;
                    }

                    systemPrompt = systemPrompt ?? config?.SystemPrompt ?? project?.SystemPrompt;
                }
                else
                {
                    // If no API key is provided, we might still allow it if the user is authenticated via JWT (Dashboard)
                    // but for the public widget, API key is required.
                    if (Context.User.Identity?.IsAuthenticated != true)
                    {
                        await Clients.Caller.SendAsync("ReceiveError", "Unauthorized: API Key required.");
                        return;
                    }
                }

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
                    project?.Id
                );

                var session = _sessionManager.GetSession(connectionId);
                if (session != null)
                {
                    session.OnError += async (error) =>
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveError", error);
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start live session for {ConnectionId}", connectionId);
                await Clients.Caller.SendAsync("ReceiveError", "Failed to connect to Gemini.");
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
