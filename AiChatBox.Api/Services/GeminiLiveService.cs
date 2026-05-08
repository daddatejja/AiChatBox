using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class GeminiLiveService(IConfiguration config, ILogger<GeminiLiveService> logger, IChatContextService contextService, IAiLoggingService aiLogger) : IGeminiLiveService
    {
        private readonly string _apiKey = config["Gemini:ApiKey"] ?? "";
        private readonly ILogger<GeminiLiveService> _logger = logger;
        private readonly IChatContextService _contextService = contextService;
        private readonly IAiLoggingService _aiLogger = aiLogger;
        
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private TaskCompletionSource<bool>? _setupTcs;
        public Guid? ProjectId { get; set; }
        public string? UserId { get; set; }

        public event Func<byte[], Task>? OnAudioReceived;
        public event Func<string, bool, Task>? OnTextReceived;
        public event Func<string, Task>? OnInputTranscribed;
        public event Func<string, string, Dictionary<string, object>, Task>? OnToolCall;
        public event Action<string>? OnError;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public async Task ConnectAsync(string userId, string? voiceName = null, string? systemPrompt = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey)) throw new Exception("Gemini API key missing");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _setupTcs = new TaskCompletionSource<bool>();
            _webSocket = new ClientWebSocket();
            
            var uri = new Uri($"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={_apiKey}");

            try
            {
                await _webSocket.ConnectAsync(uri, _cts.Token);
                _logger.LogInformation("Connected to Gemini Live API WebSocket.");

                _receiveTask = ReceiveLoopAsync(_cts.Token);
                this.UserId = userId;

                await SendSetupMessageAsync(userId, voiceName, systemPrompt, _cts.Token);
                
                // Wait for setup to complete
                await _setupTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), _cts.Token);

                // Initial Greeting
                await SendTextMessageAsync("Greetings! Start the session with a very brief time-based greeting.", _cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Gemini Live API.");
                OnError?.Invoke($"Gemini connection failed: {ex.Message}");
                throw;
            }
        }

        private async Task SendSetupMessageAsync(string userId, string? voiceName, string? customSystemPrompt, CancellationToken cancellationToken)
        {
            var systemPrompt = !string.IsNullOrEmpty(customSystemPrompt) 
                ? customSystemPrompt 
                : await _contextService.BuildSystemPromptAsync(userId);

            systemPrompt += "\n\nYOU ARE IN LIVE VOICE MODE. Be extremely concise, conversational, and proactive. " +
                            "Keep responses brief and to the point. Speak naturally as a human would.";

            var setupReq = new GeminiLiveSetupRequest
            {
                Setup = new GeminiLiveSetup
                {
                    Model = "models/gemini-2.5-flash-native-audio-latest",
                    GenerationConfig = new GeminiLiveGenerationConfig
                    {
                        ResponseModalities = ["audio"],
                        SpeechConfig = new GeminiLiveSpeechConfig
                        {
                            VoiceConfig = new GeminiLiveVoiceConfig
                            {
                                PrebuiltVoiceConfig = new GeminiLivePrebuiltVoiceConfig
                                {
                                    VoiceName = voiceName ?? "Aoede"
                                }
                            }
                        }
                    },
                    SystemInstruction = new GeminiLiveSystemInstruction
                    {
                        Parts = [new GeminiLivePart { Text = systemPrompt }]
                    },
                    InputAudioTranscription = new { },
                    OutputAudioTranscription = new { }
                }
            };

            var json = JsonSerializer.Serialize(setupReq, _jsonOptions);
            await SendJsonAsync(json, cancellationToken);
        }

        public async Task SendAudioChunkAsync(string base64Data, CancellationToken cancellationToken = default)
        {
            var req = new GeminiLiveRealtimeInputRequest
            {
                RealtimeInput = new GeminiLiveRealtimeInput
                {
                    Audio = new GeminiLiveMediaChunk
                    {
                        Data = base64Data,
                        MimeType = "audio/pcm;rate=16000"
                    }
                }
            };

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            await SendJsonAsync(json, cancellationToken);
        }

        public async Task SendTextMessageAsync(string text, CancellationToken cancellationToken = default)
        {
            var req = new GeminiLiveClientContentRequest
            {
                ClientContent = new GeminiLiveClientContent
                {
                    Turns = [new GeminiLiveTurn
                    {
                        Role = "user",
                        Parts = [new GeminiLivePart { Text = text }]
                    }],
                    TurnComplete = true
                }
            };
            var json = JsonSerializer.Serialize(req, _jsonOptions);
            await SendJsonAsync(json, cancellationToken);
        }

        public async Task CompleteTurnAsync(CancellationToken cancellationToken = default)
        {
            var req = new GeminiLiveClientContentRequest
            {
                ClientContent = new GeminiLiveClientContent
                {
                    Turns = null,
                    TurnComplete = true
                }
            };
            var json = JsonSerializer.Serialize(req, _jsonOptions);
            await SendJsonAsync(json, cancellationToken);
        }

        private async Task SendJsonAsync(string json, CancellationToken cancellationToken)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                _logger.LogWarning("WebSocket not open. State: {State}", _webSocket?.State);
                return;
            }
            
            _logger.LogDebug("Sending to Gemini: {Json}", json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 64];
            using var ms = new MemoryStream();

            try
            {
                while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("Gemini closed the connection: {Status}", result.CloseStatus);
                        break;
                    }

                    ms.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(ms.ToArray());
                        ms.SetLength(0);
                        
                        _logger.LogDebug("Received from Gemini: {Json}", json);
                        await ProcessReceivedJsonAsync(json, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Gemini WebSocket receive loop");
                OnError?.Invoke($"Gemini connection error: {ex.Message}");
            }
        }

        private async Task ProcessReceivedJsonAsync(string json, CancellationToken cancellationToken)
        {
            try
            {
                var response = JsonSerializer.Deserialize<GeminiLiveServerResponse>(json, _jsonOptions);
                if (response == null) return;

                if (response.Error != null)
                {
                    _logger.LogError("Gemini API Error: {Message}", response.Error.Message);
                    OnError?.Invoke($"Gemini API Error: {response.Error.Message}");
                    return;
                }

                if (json.Contains("setupComplete") || json.Contains("setup_complete"))
                {
                    _setupTcs?.TrySetResult(true);
                    return;
                }

                if (response.ServerContent?.ModelTurn?.Parts != null)
                {
                    foreach (var part in response.ServerContent.ModelTurn.Parts)
                    {
                        if (part.InlineData != null && !string.IsNullOrEmpty(part.InlineData.Data))
                        {
                            var audioBytes = Convert.FromBase64String(part.InlineData.Data);
                            if (OnAudioReceived != null) await OnAudioReceived.Invoke(audioBytes);
                        }
                        else if (!string.IsNullOrEmpty(part.Text))
                        {
                            if (OnTextReceived != null) await OnTextReceived.Invoke(part.Text, part.Thought ?? false);
                        }
                    }
                }

                if (response.ServerContent?.OutputTranscription != null)
                {
                    var text = response.ServerContent.OutputTranscription.Text;
                    if (!string.IsNullOrEmpty(text) && OnTextReceived != null)
                        await OnTextReceived.Invoke(text, false);
                }

                if (response.ServerContent?.InputTranscription != null)
                {
                    var text = response.ServerContent.InputTranscription.Text;
                    if (!string.IsNullOrEmpty(text) && OnInputTranscribed != null)
                        await OnInputTranscribed.Invoke(text);
                }

                if (response.ToolCall != null)
                {
                    foreach (var fc in response.ToolCall.FunctionCalls)
                    {
                        _logger.LogInformation("Tool call received: {Name}", fc.Name);
                        // Log to DB
                        await _aiLogger.LogRequestAsync(new AiRequestLog
                        {
                            ProjectId = this.ProjectId,
                            UserId = this.UserId,
                            Endpoint = "GeminiLive/ToolCall",
                            RawResponse = JsonSerializer.Serialize(fc, _jsonOptions),
                            DurationMs = 0
                        });
                        
                        if (OnToolCall != null) await OnToolCall.Invoke(fc.Id, fc.Name, fc.Args);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Gemini message");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_webSocket != null)
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    try { await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None); } catch { }
                }
                _webSocket.Dispose();
                _webSocket = null;
            }

            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
                _receiveTask = null;
            }
        }
    }
}
