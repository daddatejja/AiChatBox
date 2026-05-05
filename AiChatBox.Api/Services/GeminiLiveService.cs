using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace AiChatBox.Api.Services
{
    public class GeminiLiveService : IGeminiLiveService
    {
        private readonly string _apiKey;
        private readonly IHubContext<LiveAudioHub> _hubContext;
        private readonly ILogger<GeminiLiveService> _logger;
        private readonly ConcurrentDictionary<string, ClientWebSocket> _sockets = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cts = new();

        public GeminiLiveService(IConfiguration config, IHubContext<LiveAudioHub> hubContext, ILogger<GeminiLiveService> logger)
        {
            _apiKey = config["Gemini:ApiKey"] ?? "";
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task StartSessionAsync(string connectionId, string userId, string model = "gemini-2.5-flash-native-audio-latest")
        {
            if (string.IsNullOrEmpty(_apiKey)) throw new Exception("Gemini API key missing");

            try
            {
                var ws = new ClientWebSocket();
                var url = $"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={_apiKey}";
                
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                _sockets[connectionId] = ws;
                
                var cts = new CancellationTokenSource();
                _cts[connectionId] = cts;

                // Safeguard against null model
                model ??= "gemini-2.5-flash-native-audio-latest";
                var modelFull = model.StartsWith("models/") ? model : $"models/{model}";

                // Initial Setup
                var setup = new
                {
                    setup = new
                    {
                        model = modelFull,
                        generation_config = new { response_modalities = new[] { "AUDIO" } }
                    }
                };
                await SendJsonAsync(ws, setup, cts.Token);

                // Start listening loop
                _ = Task.Run(() => ReceiveLoopAsync(connectionId, ws, cts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Gemini Live session for user {UserId} on connection {ConnectionId}", userId, connectionId);
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveError", $"Failed to connect to Gemini: {ex.Message}");
                throw;
            }
        }

        public async Task StopSessionAsync(string connectionId)
        {
            if (_cts.TryRemove(connectionId, out var cts)) cts.Cancel();
            if (_sockets.TryRemove(connectionId, out var ws))
            {
                if (ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by user", CancellationToken.None);
                ws.Dispose();
            }
        }

        public async Task SendAudioChunkAsync(string connectionId, byte[] audioData)
        {
            if (_sockets.TryGetValue(connectionId, out var ws) && ws.State == WebSocketState.Open)
            {
                var msg = new
                {
                    realtime_input = new
                    {
                        media_chunks = new[] {
                            new {
                                data = Convert.ToBase64String(audioData),
                                mime_type = "audio/pcm;rate=16000"
                            }
                        }
                    }
                };
                await SendJsonAsync(ws, msg, _cts[connectionId].Token);
            }
        }

        public async Task SendTextMessageAsync(string connectionId, string text)
        {
            if (_sockets.TryGetValue(connectionId, out var ws) && ws.State == WebSocketState.Open)
            {
                var msg = new
                {
                    realtime_input = new
                    {
                        media_chunks = new[] {
                            new {
                                data = Convert.ToBase64String(Encoding.UTF8.GetBytes(text)),
                                mime_type = "text/plain"
                            }
                        }
                    }
                };
                await SendJsonAsync(ws, msg, _cts[connectionId].Token);
            }
        }

        private async Task SendJsonAsync(ClientWebSocket ws, object obj, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        private async Task ReceiveLoopAsync(string connectionId, ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[1024 * 64];
            using var ms = new MemoryStream();
            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;

                    ms.Seek(0, SeekOrigin.Begin);
                    using var doc = await JsonDocument.ParseAsync(ms, cancellationToken: ct);
                    ms.SetLength(0); // Reset for next message
                    
                    var root = doc.RootElement;
                    if (root.TryGetProperty("serverContent", out var serverContent))
                    {
                        if (serverContent.TryGetProperty("modelTurn", out var modelTurn))
                        {
                            if (modelTurn.TryGetProperty("parts", out var parts))
                            {
                                foreach (var part in parts.EnumerateArray())
                                {
                                    if (part.TryGetProperty("inlineData", out var inlineData))
                                    {
                                        var data = inlineData.GetProperty("data").GetString();
                                        if (!string.IsNullOrEmpty(data))
                                        {
                                            var audioBytes = Convert.FromBase64String(data);
                                            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveAudioChunk", audioBytes, ct);
                                        }
                                    }
                                    else if (part.TryGetProperty("text", out var textPart))
                                    {
                                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveTextChunk", textPart.GetString(), ct);
                                    }
                                }
                            }
                        }
                        
                        if (serverContent.TryGetProperty("interrupted", out _))
                        {
                            _logger.LogInformation("Interrupted signal received for {ConnectionId}", connectionId);
                            await _hubContext.Clients.Client(connectionId).SendAsync("StopAudio", ct);
                        }
                        
                        if (serverContent.TryGetProperty("turnComplete", out _))
                        {
                            _logger.LogDebug("Turn complete for {ConnectionId}", connectionId);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Gemini Live session cancelled for {ConnectionId}", connectionId);
            }
            catch (Exception ex)
            {
                // SocketException 995 is often a side effect of aborting the connection
                if (ex.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.OperationAborted })
                {
                    _logger.LogInformation("Gemini Live connection aborted for {ConnectionId}", connectionId);
                }
                else
                {
                    _logger.LogError(ex, "Error in Gemini Live Receive Loop for {ConnectionId}", connectionId);
                    try 
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveError", "Connection to Gemini lost.", CancellationToken.None);
                    }
                    catch { /* Best effort */ }
                }
            }
            finally
            {
                await StopSessionAsync(connectionId);
            }
        }
    }

    public class LiveAudioHub : Hub
    {
        private readonly IGeminiLiveService _liveService;
        public LiveAudioHub(IGeminiLiveService liveService) => _liveService = liveService;

        public async Task StartLive(string userId, string model) => await _liveService.StartSessionAsync(Context.ConnectionId, userId, model);
        public async Task SendAudio(byte[] data) => await _liveService.SendAudioChunkAsync(Context.ConnectionId, data);
        public async Task SendText(string text) => await _liveService.SendTextMessageAsync(Context.ConnectionId, text);
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _liveService.StopSessionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
