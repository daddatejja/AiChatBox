using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AiChatBox.Api.Services
{
    public class AgentService(LlmProviderFactory llmFactory, ToolRegistry toolRegistry, ILogger<AgentService> logger)
    {
        private readonly LlmProviderFactory _llmFactory = llmFactory;
        private readonly ToolRegistry _toolRegistry = toolRegistry;
        private readonly ILogger<AgentService> _logger = logger;

        public async IAsyncEnumerable<string> ExecuteAgentAsync(
            string provider,
            string? modelName,
            IEnumerable<GenericChatMessage> history,
            string systemPrompt,
            string userId,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var providerService = _llmFactory.GetProvider(provider);
            var tools = _toolRegistry.GetAllTools().ToList();
            var messages = history.ToList();

            // Safety limit: 5 rounds of tool calling
            for (int i = 0; i < 5; i++)
            {
                ToolCall? currentToolCall = null;
                var sb = new StringBuilder();

                await foreach (var chunk in providerService.StreamGenerateContentAsync(messages, systemPrompt, tools, modelName, ct))
                {
                    if (chunk.ToolCall != null)
                    {
                        currentToolCall = chunk.ToolCall;
                    }
                    else if (!string.IsNullOrEmpty(chunk.Text))
                    {
                        sb.Append(chunk.Text);
                        yield return chunk.Text;
                    }
                }

                if (currentToolCall == null) break;

                // Execute tool
                _logger.LogInformation("Agent calling tool: {ToolName}", currentToolCall.Name);
                var tool = _toolRegistry.GetTool(currentToolCall.Name);
                ToolResult result;
                if (tool == null)
                {
                    result = new ToolResult { ToolName = currentToolCall.Name, Error = $"Tool '{currentToolCall.Name}' not found." };
                }
                else
                {
                    result = await tool.ExecuteAsync(currentToolCall.ArgumentsJson, userId);
                }

                // Add assistant's tool call and the tool's response to conversation history
                // Note: Gemini expects specific role/parts for function calls and responses.
                // For this standalone demo, we simulate by adding them as system/model messages or updating the context.
                // A better way is to have specific GenericChatMessage types for tool calls.
                messages.Add(new GenericChatMessage { Role = "model", Content = $"Calling tool: {currentToolCall.Name} with {currentToolCall.ArgumentsJson}" });
                messages.Add(new GenericChatMessage { Role = "user", Content = $"Tool Result ({currentToolCall.Name}): {JsonSerializer.Serialize(result)}" });
                
                // Clear the string builder as we are going for another round
                sb.Clear();
            }
        }
    }
}
