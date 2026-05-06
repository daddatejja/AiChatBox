using Microsoft.AspNetCore.SignalR;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Services;

namespace AiChatBox.Api.Services
{
    public class LiveAudioHub(ILogger<LiveAudioHub> logger, LiveSessionManager sessionManager, IHubContext<LiveAudioHub> hubContext) : Hub
    {
        private readonly ILogger<LiveAudioHub> _logger = logger;
        private readonly LiveSessionManager _sessionManager = sessionManager;
        private readonly IHubContext<LiveAudioHub> _hubContext = hubContext;

        public async Task StartLive(string userId, string? voiceName = null)
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation("Client {ConnectionId} starting live session for user {UserId}", connectionId, userId);

            try
            {
                await _sessionManager.StartSessionAsync(connectionId, userId, voiceName, 
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
                    }
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
