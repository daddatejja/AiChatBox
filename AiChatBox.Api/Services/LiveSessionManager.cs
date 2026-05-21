using System.Collections.Concurrent;
using AiChatBox.Api.Interfaces;

namespace AiChatBox.Api.Services
{
    public class LiveSessionManager(IServiceScopeFactory scopeFactory, ILogger<LiveSessionManager> logger)
    {
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<LiveSessionManager> _logger = logger;
        private readonly ConcurrentDictionary<string, LiveSessionState> _sessions = new();
        private readonly ConcurrentDictionary<string, string> _userConnections = new();

        public async Task<IGeminiLiveService> StartSessionAsync(
            string connectionId, 
            string userId, 
            string? voiceName, 
            string? systemPrompt,
            Func<byte[], Task> onAudioReceived, 
            Func<string, bool, Task> onTextReceived, 
            Func<string, Task> onInputTranscribed,
            Func<string, string, Dictionary<string, object>, bool, Task> onToolCall,
            Func<string, string, object, Task> onToolResult,
            Guid? projectId = null,
            Guid? configurationId = null,
            string? apiKeyOverride = null,
            Guid? sessionId = null)
        {
            // Enforce 1 live session per user
            if (_userConnections.TryGetValue(userId, out var existingConnectionId))
            {
                _logger.LogInformation("User {UserId} started a new live session. Stopping previous session {ConnectionId}.", userId, existingConnectionId);
                await StopSessionAsync(existingConnectionId);
            }

            var scope = _scopeFactory.CreateAsyncScope();
            try
            {
                var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiLiveService>();
                geminiService.OnAudioReceived += onAudioReceived;
                geminiService.OnTextReceived += onTextReceived;
                geminiService.OnInputTranscribed += onInputTranscribed;
                geminiService.OnToolCall += onToolCall;
                geminiService.OnToolResult += onToolResult;
                geminiService.ProjectId = projectId;
                geminiService.ConfigurationId = configurationId;
                geminiService.SessionId = sessionId;
                geminiService.ApiKeyOverride = apiKeyOverride;
                geminiService.UserId = userId; // Ensure UserId is set on the service

                await geminiService.ConnectAsync(userId, voiceName, systemPrompt);

                _sessions[connectionId] = new LiveSessionState(geminiService, scope);
                _userConnections[userId] = connectionId;
                return geminiService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Gemini Live session for {ConnectionId}", connectionId);
                await scope.DisposeAsync();
                throw;
            }
        }

        public IGeminiLiveService? GetSession(string connectionId)
        {
            if (_sessions.TryGetValue(connectionId, out var state))
            {
                return state.Service;
            }
            return null;
        }

        public async Task StopSessionAsync(string connectionId)
        {
            if (_sessions.TryRemove(connectionId, out var state))
            {
                try
                {
                    await state.Service.DisposeAsync();
                }
                finally
                {
                    await state.Scope.DisposeAsync();
                }

                if (state.Service.UserId != null && _userConnections.TryGetValue(state.Service.UserId, out var currentConn) && currentConn == connectionId)
                {
                    _userConnections.TryRemove(state.Service.UserId, out _);
                }

                _logger.LogInformation("Stopped session for {ConnectionId}", connectionId);
            }
        }

        private record LiveSessionState(IGeminiLiveService Service, AsyncServiceScope Scope);
    }
}
