using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    public class AgentService(LlmProviderFactory llmFactory, 
                            ToolRegistry toolRegistry, 
                            WebhookService webhookService,
                            IAiLoggingService aiLogger,
                            ILogger<AgentService> logger)
    {
        private readonly LlmProviderFactory _llmFactory = llmFactory;
        private readonly ToolRegistry _toolRegistry = toolRegistry;
        private readonly WebhookService _webhookService = webhookService;
        private readonly IAiLoggingService _aiLogger = aiLogger;
        private readonly ILogger<AgentService> _logger = logger;

        public async IAsyncEnumerable<AgentChunk> ExecuteAgentAsync(
            string provider,
            string? modelName,
            List<GenericChatMessage> history,
            string systemPrompt,
            string userId,
            Project? project,
            string? apiKeyOverride = null,
            IEnumerable<ITool>? extraTools = null,
            Guid? sessionId = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var providerService = _llmFactory.GetProvider(provider);
            
            // Combine built-in tools with custom tools from project
            var allTools = new List<ITool>(_toolRegistry.GetAllTools());
            
            if (extraTools != null)
            {
                allTools.AddRange(extraTools);
            }
            if (project != null)
            {
                foreach (var ctModel in project.CustomTools.Where(t => t.IsActive))
                {
                    allTools.Add(new DynamicTool(ctModel, project, _webhookService));
                }
            }

            var messages = history.ToList();
            var reachedMaxIterations = true;

            for (int i = 0; i < 5; i++)
            {
                ToolCall? currentToolCall = null;
                var sb = new StringBuilder();

                await foreach (var chunk in providerService.StreamGenerateContentAsync(messages, systemPrompt, allTools, modelName, apiKeyOverride, ct))
                {
                    if (chunk.ToolCall != null)
                    {
                        currentToolCall = chunk.ToolCall;
                    }
                    else if (!string.IsNullOrEmpty(chunk.Text))
                    {
                        sb.Append(chunk.Text);
                        yield return new AgentChunk { Text = chunk.Text };
                    }
                }

                if (currentToolCall == null) { reachedMaxIterations = false; break; }

                _logger.LogInformation("Agent calling tool: {ToolName}", currentToolCall.Name);

                var tool = allTools.FirstOrDefault(t => t.Name == currentToolCall.Name);
                
                var customTool = project?.CustomTools.FirstOrDefault(t => t.Name == currentToolCall.Name);
                if (customTool != null && string.IsNullOrEmpty(project?.WebhookUrl))
                {
                    yield return new AgentChunk 
                    { 
                        ToolCall = new ToolCallDto 
                        { 
                            Id = currentToolCall.Id,
                            Name = currentToolCall.Name, 
                            Arguments = currentToolCall.ArgumentsJson 
                        } 
                    };
                    yield break; // Stop execution, let client handle it and come back
                }

                ToolResult result;
                var toolStartTime = DateTime.UtcNow;
                if (tool == null)
                {
                    result = new ToolResult { ToolName = currentToolCall.Name, Error = $"Tool '{currentToolCall.Name}' not found." };
                }
                else
                {
                    result = await tool.ExecuteAsync(currentToolCall.ArgumentsJson, userId);
                }

                var duration = (int)(DateTime.UtcNow - toolStartTime).TotalMilliseconds;
                await _aiLogger.LogRequestAsync(new AiRequestLog
                {
                    ProjectId = project?.Id,
                    SessionId = sessionId,
                    UserId = userId,
                    Provider = provider,
                    Model = modelName,
                    Endpoint = $"Tool: {currentToolCall.Name}",
                    RawRequest = currentToolCall.ArgumentsJson,
                    RawResponse = result.Content?.ToString(),
                    ErrorMessage = result.Error,
                    DurationMs = duration
                });

                messages.Add(new GenericChatMessage 
                { 
                    Role = "model", 
                    Content = JsonSerializer.Serialize(new { toolCall = new { id = currentToolCall.Id, name = currentToolCall.Name, args = JsonNode.Parse(currentToolCall.ArgumentsJson) } }) 
                });
                
                messages.Add(new GenericChatMessage 
                { 
                    Role = "function", 
                    Content = JsonSerializer.Serialize(new { toolCallId = currentToolCall.Id, toolName = currentToolCall.Name, result = result.Content }) 
                });
                
                sb.Clear();
            }

            if (reachedMaxIterations)
            {
                yield return new AgentChunk { Text = "\n\n*Maximum tool-calling iterations reached. Task may be incomplete.*" };
            }
        }
    }

    public class DynamicTool : ITool
    {
        private readonly CustomTool _model;
        private readonly Project _project;
        private readonly WebhookService _webhookService;

        public DynamicTool(CustomTool model, Project project, WebhookService webhookService)
        {
            _model = model;
            _project = project;
            _webhookService = webhookService;
        }

        public string Name => _model.Name;
        public string Description => _model.Description;
        public JsonObject ParametersSchema => JsonNode.Parse(_model.ParametersJsonSchema)!.AsObject();

        public async Task<ToolResult> ExecuteAsync(string argumentsJson, string userId)
        {
            return await _webhookService.ExecuteWebhookToolAsync(_project, _model.Name, argumentsJson);
        }
    }
}
