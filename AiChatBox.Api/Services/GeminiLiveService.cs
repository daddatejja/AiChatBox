using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using AiChatBox.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class GeminiLiveService(IConfiguration config, 
                                   ILogger<GeminiLiveService> logger, 
                                   IChatContextService contextService, 
                                   IAiLoggingService aiLogger,
                                   IAiChatService chatService,
                                   IDbContextFactory<ChatDbContext> dbFactory,
                                   IEmbeddingService embeddingService,
                                   WebhookService webhookService,
                                   ToolRegistry toolRegistry) : IGeminiLiveService
    {
        private readonly string _apiKey = config["Gemini:ApiKey"] ?? "";
        private readonly ILogger<GeminiLiveService> _logger = logger;
        private readonly IChatContextService _contextService = contextService;
        private readonly IAiLoggingService _aiLogger = aiLogger;
        private readonly IAiChatService _chatService = chatService;
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly IEmbeddingService _embeddingService = embeddingService;
        private readonly WebhookService _webhookService = webhookService;
        private readonly ToolRegistry _toolRegistry = toolRegistry;
        
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private TaskCompletionSource<bool>? _setupTcs;
        private bool _disposed = false;
        private readonly StringBuilder _sessionInput = new();
        private readonly StringBuilder _sessionOutput = new();
        private readonly DateTime _sessionStart = DateTime.UtcNow;
        public Guid? ProjectId { get; set; }
        public Guid? ConfigurationId { get; set; }
        public Guid? SessionId { get; set; }
        public Guid? ParentSessionId { get; set; }
        public string? UserId { get; set; }
        public string? ApiKeyOverride { get; set; }

        public event Func<byte[], Task>? OnAudioReceived;
        public event Func<string, bool, Task>? OnTextReceived;
        public event Func<string, Task>? OnInputTranscribed;
        public event Func<string, string, Dictionary<string, object>, bool, Task>? OnToolCall;
        public event Func<string, string, object, Task>? OnToolResult;
        public event Action<string>? OnError;
        public event Action<string>? OnDisconnected;

        private readonly List<TimelineEvent> _timelineEvents = new();
        private readonly MemoryStream _userAudioBuffer = new();
        private readonly MemoryStream _modelAudioBuffer = new();
        private readonly StringBuilder _modelTranscriptionBuffer = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public async Task ConnectAsync(string userId, string? voiceName = null, string? systemPrompt = null, CancellationToken cancellationToken = default)
        {
            var apiKey = !string.IsNullOrEmpty(ApiKeyOverride) ? ApiKeyOverride : _apiKey;
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("Gemini API key missing");

            if (!string.IsNullOrEmpty(ApiKeyOverride))
                _logger.LogInformation("Connecting to Gemini Live with override key (ends with ...{KeyTail})", apiKey.Substring(Math.Max(0, apiKey.Length - 4)));
            else
                _logger.LogInformation("Connecting to Gemini Live with global key (ends with ...{KeyTail})", apiKey.Substring(Math.Max(0, apiKey.Length - 4)));

            // Ensure session exists in DB before we start logging
            if (this.SessionId != null)
            {
                var session = await _chatService.GetOrCreateSessionAsync(userId, this.SessionId, this.ProjectId, this.ConfigurationId, "live_voice", this.ParentSessionId);
                this.SessionId = session.Id;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _setupTcs = new TaskCompletionSource<bool>();
            _webSocket = new ClientWebSocket();
            
            var uri = new Uri($"wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent?key={apiKey}");

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
                
                try
                {
                    await _aiLogger.LogRequestAsync(new AiRequestLog
                    {
                        ProjectId = this.ProjectId,
                        ConfigurationId = this.ConfigurationId,
                        SessionId = this.SessionId,
                        UserId = this.UserId,
                        Provider = "gemini",
                        Model = "gemini-2.5-flash-native-audio-latest",
                        Endpoint = "GeminiLive/Connect",
                        ErrorMessage = ex.Message,
                        DurationMs = (int)(DateTime.UtcNow - _sessionStart).TotalMilliseconds
                    });
                }
                catch { }

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
                            "Keep responses brief and to the point. Speak naturally as a human would. " +
                            "When you execute data-related tools (like SQL queries), the results will be rendered as interactive tables/charts in the user's transcript. " +
                            "You can tell the user they can see the data there and even export it to PDF or Excel using the buttons on the widget.";

            var tools = await BuildToolDeclarations();
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
                    Tools = tools,
                    InputAudioTranscription = new { },
                    OutputAudioTranscription = new { }
                }
            };

            var json = JsonSerializer.Serialize(setupReq, _jsonOptions);
            await SendJsonAsync(json, cancellationToken);
        }

        public async Task SendAudioChunkAsync(string base64Data, CancellationToken cancellationToken = default)
        {
            var bytes = Convert.FromBase64String(base64Data);
            _userAudioBuffer.Write(bytes, 0, bytes.Length);

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
            _timelineEvents.Add(new TimelineEvent { Type = "UserText", Content = text });

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
                _logger.LogError("WebSocket not open for Send. State: {State}", _webSocket?.State);
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
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        _logger.LogInformation("Gemini closed the connection: {Status}", result.CloseStatus);
                        OnDisconnected?.Invoke($"Gemini closed the connection: {result.CloseStatus}");
                        break;
                    }

                    ms.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        var json = Encoding.UTF8.GetString(ms.ToArray());
                        ms.SetLength(0);
                        
                        _logger.LogInformation("Received from Gemini: {Json}", json);
                        await ProcessReceivedJsonAsync(json, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Gemini WebSocket receive loop");
                OnError?.Invoke($"Gemini connection error: {ex.Message}");
                OnDisconnected?.Invoke("Connection to Gemini was lost.");
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
                    
                    try
                    {
                        await _aiLogger.LogRequestAsync(new AiRequestLog
                        {
                            ProjectId = this.ProjectId,
                            ConfigurationId = this.ConfigurationId,
                            UserId = this.UserId,
                            Provider = "gemini",
                            Model = "gemini-2.5-flash-native-audio-latest",
                            Endpoint = "GeminiLive/ApiError",
                            ErrorMessage = response.Error.Message,
                            RawResponse = json
                        });
                    }
                    catch { }

                    OnError?.Invoke($"Gemini API Error: {response.Error.Message}");
                    return;
                }

                if (json.Contains("setupComplete") || json.Contains("setup_complete"))
                {
                    _logger.LogInformation("Gemini Live Session Setup Complete for User: {UserId}", UserId);
                    _timelineEvents.Add(new TimelineEvent { Type = "System", Content = "Session Setup Complete" });
                    _setupTcs?.TrySetResult(true);
                    return;
                }

                if (response.ServerContent?.ModelTurn?.Parts != null)
                {
                    foreach (var part in response.ServerContent.ModelTurn.Parts)
                    {
                        if (part.InlineData != null && !string.IsNullOrEmpty(part.InlineData.Data))
                        {
                            _logger.LogInformation("Received audio chunk ({Size} bytes)", part.InlineData.Data.Length);
                            var audioBytes = Convert.FromBase64String(part.InlineData.Data);
                            _modelAudioBuffer.Write(audioBytes, 0, audioBytes.Length);
                            if (OnAudioReceived != null) await OnAudioReceived.Invoke(audioBytes);
                        }
                        else if (!string.IsNullOrEmpty(part.Text))
                        {
                            _logger.LogInformation("Gemini Response Text: {Text}", part.Text);
                            _sessionOutput.AppendLine($"[Model]: {part.Text}");
                            
                            _timelineEvents.Add(new TimelineEvent { 
                                Type = part.Thought == true ? "ModelThinking" : "ModelText", 
                                Content = part.Text 
                            });

                            if (OnTextReceived != null) await OnTextReceived.Invoke(part.Text, part.Thought ?? false);
                        }
                    }
                }

                if (response.ServerContent?.OutputTranscription != null)
                {
                    var text = response.ServerContent.OutputTranscription.Text;
                    _logger.LogInformation("Model Speech Transcription: {Text}", text);
                    _sessionOutput.AppendLine($"[Transcription]: {text}");
                    _modelTranscriptionBuffer.Append(text).Append(" ");

                    if (!string.IsNullOrEmpty(text) && OnTextReceived != null)
                        await OnTextReceived.Invoke(text, false);
                }

                if (response.ServerContent?.TurnComplete != null)
                {
                    if (_modelAudioBuffer.Length > 0)
                    {
                        var audioBytes = _modelAudioBuffer.ToArray();
                        var base64 = Convert.ToBase64String(audioBytes);
                        var trans = _modelTranscriptionBuffer.ToString().Trim();
                        if (string.IsNullOrEmpty(trans)) trans = "(untranscribed)";
                        _timelineEvents.Add(new TimelineEvent { Type = "ModelAudio", Content = base64, Transcription = trans });
                        _modelAudioBuffer.SetLength(0);
                        _modelTranscriptionBuffer.Clear();
                    }
                }

                if (response.ServerContent?.InputTranscription != null)
                {
                    var text = response.ServerContent.InputTranscription.Text;
                    _logger.LogInformation("User Speech Transcription: {Text}", text);
                    _sessionInput.AppendLine(text);
                    
                    if (_userAudioBuffer.Length > 0)
                    {
                        var audioBytes = _userAudioBuffer.ToArray();
                        var base64 = Convert.ToBase64String(audioBytes);
                        _timelineEvents.Add(new TimelineEvent { Type = "UserAudio", Content = base64, Transcription = text });
                        _userAudioBuffer.SetLength(0);
                    }

                    if (OnInputTranscribed != null) await OnInputTranscribed.Invoke(text);
                }

                if (response.ToolCall != null)
                {
                    _logger.LogInformation("Processing {Count} tool calls in parallel", response.ToolCall.FunctionCalls.Length);
                    
                    var tools = await GetProjectToolsAsync();
                    var tasks = response.ToolCall.FunctionCalls.Select(async fc => 
                    {
                        var result = await ExecuteToolInternalAsync(fc, tools, cancellationToken);
                        return new GeminiLiveFunctionResponse
                        {
                            Id = fc.Id,
                            Name = fc.Name,
                            Response = new { result = result }
                        };
                    });

                    var results = await Task.WhenAll(tasks);

                    var req = new GeminiLiveClientContentRequest
                    {
                        ToolResponse = new GeminiLiveToolResponse
                        {
                            FunctionResponses = results
                        }
                    };
                    var jsonResp = JsonSerializer.Serialize(req, _jsonOptions);
                    await SendJsonAsync(jsonResp, cancellationToken);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Gemini message");
            }
        }

        private async Task<List<ITool>> GetProjectToolsAsync()
        {
            var tools = new List<ITool>(_toolRegistry.GetAllTools());
            if (ProjectId.HasValue)
            {
                var apiKey = !string.IsNullOrEmpty(ApiKeyOverride) ? ApiKeyOverride : _apiKey;
                tools.Add(new Services.Tools.KnowledgeSearchTool(_dbFactory, _embeddingService, ProjectId.Value, apiKey));

                using var db = _dbFactory.CreateDbContext();
                var project = await db.Projects.Include(p => p.CustomTools).FirstOrDefaultAsync(p => p.Id == ProjectId.Value);
                if (project != null)
                {
                    foreach (var tool in project.CustomTools.Where(t => t.IsActive))
                    {
                        tools.Add(new AgentService.DynamicTool(tool, project, _webhookService));
                    }
                }
            }
            return tools;
        }

        private async Task<object[]> BuildToolDeclarations()
        {
            var tools = await GetProjectToolsAsync();
            return
            [
                new
                {
                    function_declarations = tools.Select(t => new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = t.ParametersSchema
                    }).ToArray()
                }
            ];
        }

        private async Task<object> ExecuteToolInternalAsync(GeminiLiveFunctionCall fc, List<ITool> tools, CancellationToken cancellationToken)
        {
            var argsJson = JsonSerializer.Serialize(fc.Args, _jsonOptions);
            _timelineEvents.Add(new TimelineEvent { Type = "ToolCall", Meta = fc.Name, Content = argsJson });
            
            if (OnToolCall != null) await OnToolCall.Invoke(fc.Id, fc.Name, fc.Args, true);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            AiRequestLog? log = null;
            try
            {
                var tool = tools.FirstOrDefault(t => t.Name == fc.Name);
                if (tool == null) 
                {
                    var error = $"Tool '{fc.Name}' not found.";
                    await LogToolResultAsync(fc.Name, argsJson, null, error, 0);
                    return new { error };
                }

                var result = await tool.ExecuteAsync(argsJson, UserId ?? "live-user");
                sw.Stop();
                
                var responseContent = result.Content ?? result.Error;
                var responseJson = JsonSerializer.Serialize(responseContent, _jsonOptions);
                
                _timelineEvents.Add(new TimelineEvent { Type = "ToolResponse", Meta = fc.Name, Content = responseJson });
                if (OnToolResult != null) await OnToolResult.Invoke(fc.Id, fc.Name, responseContent);

                await LogToolResultAsync(fc.Name, argsJson, responseJson, result.Error, sw.ElapsedMilliseconds);

                return responseContent;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Failed to execute tool {ToolName} in Live mode", fc.Name);
                _timelineEvents.Add(new TimelineEvent { Type = "ToolResponse", Meta = fc.Name, Content = ex.Message });
                
                await LogToolResultAsync(fc.Name, argsJson, null, ex.Message, sw.ElapsedMilliseconds);
                
                return new { error = ex.Message };
            }
        }

        private async Task LogToolResultAsync(string name, string request, string? response, string? error, long duration)
        {
            try
            {
                await _aiLogger.LogRequestAsync(new AiRequestLog
                {
                    ProjectId = this.ProjectId,
                    ConfigurationId = this.ConfigurationId,
                    SessionId = this.SessionId,
                    UserId = this.UserId,
                    Provider = "gemini",
                    Model = "models/gemini-2.5-flash-native-audio-latest",
                    Endpoint = $"Live Tool: {name}",
                    RawRequest = request,
                    RawResponse = response,
                    ErrorMessage = error,
                    DurationMs = (int)duration
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log live tool result");
            }
        }

        public async Task SendToolResponseAsync(string id, string name, object response, CancellationToken cancellationToken = default)
        {
            var req = new GeminiLiveClientContentRequest
            {
                ToolResponse = new GeminiLiveToolResponse
                {
                    FunctionResponses = [new GeminiLiveFunctionResponse
                    {
                        Id = id,
                        Name = name,
                        Response = response
                    }]
                }
            };
            var json = JsonSerializer.Serialize(req, _jsonOptions);
            await SendJsonAsync(json, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            // Ensure any un-transcribed buffers are captured before disposing
            if (_userAudioBuffer.Length > 0)
            {
                var base64 = Convert.ToBase64String(_userAudioBuffer.ToArray());
                _timelineEvents.Add(new TimelineEvent { Type = "UserAudio", Content = base64, Transcription = "(untranscribed)" });
            }
            if (_modelAudioBuffer.Length > 0)
            {
                var base64 = Convert.ToBase64String(_modelAudioBuffer.ToArray());
                _timelineEvents.Add(new TimelineEvent { Type = "ModelAudio", Content = base64, Transcription = "(untranscribed)" });
            }

            if (_timelineEvents.Count > 0 || _sessionInput.Length > 0 || _sessionOutput.Length > 0)
            {
                try
                {
                    await _aiLogger.LogRequestAsync(new AiRequestLog
                    {
                        ProjectId = this.ProjectId,
                        ConfigurationId = this.ConfigurationId,
                        SessionId = this.SessionId,
                        UserId = this.UserId,
                        Provider = "gemini",
                        Model = "gemini-2.5-flash-native-audio-latest",
                        Endpoint = "GeminiLive/Session",
                        RawRequest = _sessionInput.ToString(),
                        RawResponse = JsonSerializer.Serialize(_timelineEvents, _jsonOptions),
                        DurationMs = (int)(DateTime.UtcNow - _sessionStart).TotalMilliseconds
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to log Gemini Live session to DB");
                }
            }

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

            _userAudioBuffer.Dispose();
            _modelAudioBuffer.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
