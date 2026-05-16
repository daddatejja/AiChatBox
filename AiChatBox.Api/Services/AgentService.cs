using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services.Tools;

namespace AiChatBox.Api.Services
{
    public class AgentService(LlmProviderFactory llmFactory, 
                            ToolRegistry toolRegistry, 
                            WebhookService webhookService,
                            IAiLoggingService aiLogger,
                            EncryptionService encryptionService,
                            ILogger<AgentService> logger)
    {
        private readonly LlmProviderFactory _llmFactory = llmFactory;
        private readonly ToolRegistry _toolRegistry = toolRegistry;
        private readonly WebhookService _webhookService = webhookService;
        private readonly IAiLoggingService _aiLogger = aiLogger;
        private readonly EncryptionService _encryption = encryptionService;
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

                if (project.Database != null)
                {
                    var decryptedConn = _encryption.Decrypt(project.Database.ConnectionString);
                    if (!string.IsNullOrEmpty(decryptedConn))
                    {
                        allTools.Add(new UserSqlTool(project.Database, decryptedConn));
                        
                        var schema = GetCompactSchema(project.Database.SchemaDefinition);
                        if (!string.IsNullOrEmpty(schema))
                        {
                            var dbPrompt = $"\n\n[DATABASE SCHEMA (COMPACT)]\n{schema}\n\nUse this schema to write SQL queries. If the user asks for analytics, reports, or files (PDF/Excel), you MUST use the 'query_project_database' tool.";
                            dbPrompt += "\n\nWhen you use this tool, a rich interactive widget will be displayed to the user automatically. You do NOT need to write JSON yourself or describe the process.";
                            dbPrompt += "\nIMPORTANT: Do NOT re-type the data as a Markdown table or JSON block. Simply provide a brief summary of what you found and refer the user to the interactive widget below.";
                            
                            if (project.Database.Type == DatabaseType.PostgreSQL)
                            {
                                dbPrompt += "\nIMPORTANT: This is a PostgreSQL database. You MUST use double quotes for all mixed-case table and column names (e.g., SELECT * FROM \"AiRequestLogs\"). If you encounter 'relation does not exist' errors, it is usually because of missing quotes around a mixed-case table name.";
                            }
                            
                            systemPrompt += dbPrompt;
                        }
                    }
                }
            }

            var messages = CleanHistory(history);
            var reachedMaxIterations = true;

            for (int i = 0; i < 5; i++)
            {
                var accumulatedToolCalls = new List<ToolCall>();
                var sb = new StringBuilder();

                await foreach (var chunk in providerService.StreamGenerateContentAsync(messages, systemPrompt, allTools, modelName, apiKeyOverride, ct))
                {
                    if (chunk.ToolCalls != null && chunk.ToolCalls.Count > 0)
                    {
                        foreach (var tc in chunk.ToolCalls)
                        {
                            var existing = accumulatedToolCalls.FirstOrDefault(x => x.Id == tc.Id);
                            if (existing == null)
                            {
                                accumulatedToolCalls.Add(tc);
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(tc.ArgumentsJson))
                                {
                                    // Append if streaming arguments (OpenAI/Groq style)
                                    existing.ArgumentsJson += tc.ArgumentsJson;
                                }
                                if (tc.ThoughtSignature != null)
                                    existing.ThoughtSignature = tc.ThoughtSignature;
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(chunk.Text))
                    {
                        sb.Append(chunk.Text);
                        yield return new AgentChunk { Text = chunk.Text };
                    }
                }

                if (accumulatedToolCalls.Count == 0) { reachedMaxIterations = false; break; }

                // Separate client tools from backend tools
                var clientToolCalls = new List<ToolCall>();
                var backendToolCalls = new List<ToolCall>();

                foreach (var tc in accumulatedToolCalls)
                {
                    var customTool = project?.CustomTools.FirstOrDefault(t => t.Name == tc.Name);
                    if (customTool != null && string.IsNullOrEmpty(project?.WebhookUrl))
                    {
                        clientToolCalls.Add(tc);
                    }
                    else
                    {
                        backendToolCalls.Add(tc);
                    }
                }

                if (clientToolCalls.Count > 0)
                {
                    // Yield ALL client tool calls as a single chunk
                    yield return new AgentChunk
                    {
                        ToolCalls = clientToolCalls.Select(tc => new ToolCallDto
                        {
                            Id = tc.Id,
                            Name = tc.Name,
                            Arguments = tc.ArgumentsJson,
                            ThoughtSignature = tc.ThoughtSignature
                        }).ToList()
                    };
                    yield break; 
                }

                // Add the model's calls to history
                messages.Add(new GenericChatMessage
                {
                    Role = "model",
                    Content = JsonSerializer.Serialize(new
                    {
                        toolCalls = accumulatedToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            name = tc.Name,
                            args = TryParseJson(tc.ArgumentsJson),
                            thoughtSignature = tc.ThoughtSignature
                        })
                    })
                });

                // Execute ALL backend tools in parallel
                var toolTasks = backendToolCalls.Select(async tc =>
                {
                    var toolName = tc.Name;
                    if (toolName.StartsWith("default_api:")) toolName = toolName.Substring("default_api:".Length);
                    
                    var tool = allTools.FirstOrDefault(t => t.Name == toolName);
                    var toolStartTime = DateTime.UtcNow;
                    ToolResult result;

                    if (tool == null)
                    {
                        result = new ToolResult { ToolName = tc.Name, Error = $"Tool '{tc.Name}' not found." };
                    }
                    else
                    {
                        try {
                            result = await tool.ExecuteAsync(tc.ArgumentsJson, userId);
                        } catch (Exception ex) {
                            result = new ToolResult { ToolName = tc.Name, Error = ex.Message };
                        }
                    }

                    var duration = (int)(DateTime.UtcNow - toolStartTime).TotalMilliseconds;
                    await _aiLogger.LogRequestAsync(new AiRequestLog
                    {
                        ProjectId = project?.Id,
                        SessionId = sessionId,
                        UserId = userId,
                        Provider = provider,
                        Model = modelName,
                        Endpoint = $"Tool: {tc.Name}",
                        RawRequest = tc.ArgumentsJson,
                        RawResponse = result.Content?.ToString(),
                        ErrorMessage = result.Error,
                        DurationMs = duration
                    });

                    return (tc, result);
                }).ToList();

                var executedResults = await Task.WhenAll(toolTasks);

                foreach (var (tc, res) in executedResults)
                {
                    messages.Add(new GenericChatMessage
                    {
                        Role = "function",
                        Content = JsonSerializer.Serialize(new { toolCallId = tc.Id, toolName = tc.Name, result = res.Content ?? res.Error, thoughtSignature = tc.ThoughtSignature })
                    });

                    yield return new AgentChunk
                    {
                        ToolResult = new ToolResultDto
                        {
                            ToolName = tc.Name,
                            Result = res.Content ?? res.Error,
                            ThoughtSignature = tc.ThoughtSignature
                        }
                    };
                }

                sb.Clear();
            }

            if (reachedMaxIterations)
            {
                yield return new AgentChunk { Text = "\n\n*Maximum tool-calling iterations reached. Task may be incomplete.*" };
            }
        }

        private string GetCompactSchema(string? ddl)
        {
            if (string.IsNullOrWhiteSpace(ddl)) return string.Empty;
            
            var sb = new StringBuilder();
            var lines = ddl.Split('\n');
            string? currentTable = null;
            var columns = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentTable != null)
                    {
                        sb.AppendLine($"{currentTable} ({string.Join(", ", columns)})");
                        columns.Clear();
                    }
                    var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"CREATE TABLE\s+""?([^""\s(]+)""?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    currentTable = match.Success ? match.Groups[1].Value : "UnknownTable";
                }
                else if (trimmed.StartsWith(");"))
                {
                    if (currentTable != null)
                    {
                        sb.AppendLine($"{currentTable} ({string.Join(", ", columns)})");
                        currentTable = null;
                        columns.Clear();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trimmed) && currentTable != null)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^""?([^""\s,]+)""?");
                    if (match.Success)
                    {
                        columns.Add(match.Groups[1].Value);
                    }
                }
            }
            
            if (currentTable != null)
            {
                sb.AppendLine($"{currentTable} ({string.Join(", ", columns)})");
            }

            return sb.ToString();
        }

        private List<GenericChatMessage> CleanHistory(List<GenericChatMessage> history)
        {
            var cleaned = history.ToList();
            if (cleaned.Count == 0) return cleaned;

            for (int i = cleaned.Count - 1; i >= 0; i--)
            {
                var msg = cleaned[i];
                var role = msg.Role?.ToLower();
                
                if (role == "model" || role == "assistant")
                {
                    if (!string.IsNullOrEmpty(msg.Content) && msg.Content.TrimStart().StartsWith("{"))
                    {
                        bool hasResponse = false;
                        for (int j = i + 1; j < cleaned.Count; j++)
                        {
                            var nextRole = cleaned[j].Role?.ToLower();
                            if (nextRole == "function" || nextRole == "tool")
                            {
                                hasResponse = true;
                                break;
                            }
                        }
                        
                        if (!hasResponse)
                        {
                            _logger.LogWarning("Removing dangling tool call from history to maintain provider turn order.");
                            cleaned.RemoveAt(i);
                        }
                    }
                }
            }
            
            return cleaned;
        }
        private static object? TryParseJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonNode.Parse(json); }
            catch { return json; }
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
                return await _webhookService.ExecuteWebhookToolAsync(_project, _model.Name, argumentsJson, _model.ParametersJsonSchema);
            }
        }
    }
}

    

