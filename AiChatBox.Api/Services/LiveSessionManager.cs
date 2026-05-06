using System.Collections.Concurrent;
using AiChatBox.Api.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiChatBox.Api.Services
{
    public class LiveSessionManager(IServiceScopeFactory scopeFactory, ILogger<LiveSessionManager> logger)
    {
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<LiveSessionManager> _logger = logger;
        private readonly ConcurrentDictionary<string, LiveSessionState> _sessions = new();

        public async Task<IGeminiLiveService> StartSessionAsync(
            string connectionId, 
            string userId, 
            string? voiceName, 
            Func<byte[], Task> onAudioReceived, 
            Func<string, bool, Task> onTextReceived, 
            Func<string, Task> onInputTranscribed)
        {
            var scope = _scopeFactory.CreateScope();
            try
            {
                var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiLiveService>();
                geminiService.OnAudioReceived += onAudioReceived;
                geminiService.OnTextReceived += onTextReceived;
                geminiService.OnInputTranscribed += onInputTranscribed;

                await geminiService.ConnectAsync(userId, voiceName);

                _sessions[connectionId] = new LiveSessionState(geminiService, scope);
                return geminiService;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Gemini Live session for {ConnectionId}", connectionId);
                if (scope is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else
                    scope.Dispose();
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
                    if (state.Scope is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync();
                    else
                        state.Scope.Dispose();
                }
                _logger.LogInformation("Stopped session for {ConnectionId}", connectionId);
            }
        }

        private record LiveSessionState(IGeminiLiveService Service, IServiceScope Scope);
    }
}
