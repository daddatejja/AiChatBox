using System.Text.Json;
using System.Text.RegularExpressions;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

using AiChatBox.Api.Interfaces;
using AiChatBox.Api.DTOs;

namespace AiChatBox.Api.Services
{
    public class FlowExecutionService(
        ChatDbContext db,
        ILogger<FlowExecutionService> logger,
        LlmProviderFactory llmFactory)
    {
        private readonly ChatDbContext _db = db;
        private readonly ILogger<FlowExecutionService> _logger = logger;
        private readonly LlmProviderFactory _llmFactory = llmFactory;

        /// <summary>
        /// Checks if the user message triggers any active flow for the project.
        /// If it does, sets the session's active flow and starts execution.
        /// </summary>
        public async Task<bool> TryTriggerFlowAsync(ChatSession session, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage) || session.ProjectId == null)
                return false;

            var flows = await _db.ConversationFlows
                .Where(f => f.ProjectId == session.ProjectId && f.IsActive && !string.IsNullOrEmpty(f.TriggerKeyword))
                .ToListAsync();

            var normalizedMsg = userMessage.Trim().ToLowerInvariant();
            var matchedFlow = flows.FirstOrDefault(f => normalizedMsg.Contains(f.TriggerKeyword!.ToLowerInvariant()));

            if (matchedFlow != null)
            {
                // Find the starting node (trigger type)
                var triggerNode = await _db.FlowNodes
                    .FirstOrDefaultAsync(n => n.FlowId == matchedFlow.Id && n.Type == "trigger");

                if (triggerNode != null)
                {
                    session.ActiveFlowId = matchedFlow.Id;
                    session.CurrentNodeId = triggerNode.Id;
                    await _db.SaveChangesAsync();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Executes the current step of the active flow.
        /// Returns the text to send back to the user, or null if the flow is yielding/waiting.
        /// If the flow ends, it clears ActiveFlowId.
        /// </summary>
        public async IAsyncEnumerable<string> ExecuteFlowStepAsync(ChatSession session, string userMessage, Project project)
        {
            if (session.ActiveFlowId == null || session.CurrentNodeId == null)
                yield break;

            int safetyCounter = 0;

            while (session.ActiveFlowId != null && safetyCounter < 10)
            {
                safetyCounter++;

                var currentNode = await _db.FlowNodes.FirstOrDefaultAsync(n => n.Id == session.CurrentNodeId);
                if (currentNode == null)
                {
                    // Invalid state, clear flow
                    session.ActiveFlowId = null;
                    session.CurrentNodeId = null;
                    await _db.SaveChangesAsync();
                    yield break;
                }

                // Execute the logic of the current node
                string? responseText = null;
                bool shouldWait = false;

                switch (currentNode.Type.ToLowerInvariant())
                {
                    case "trigger":
                        // Just an entry point, move to next immediately
                        break;

                    case "message":
                        // Output static text
                        var msgData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                        if (msgData != null && msgData.TryGetValue("config", out var configElement) && configElement.ValueKind == JsonValueKind.Object)
                        {
                            if (configElement.TryGetProperty("text", out var textProp))
                            {
                                responseText = InterpolateVariables(textProp.GetString() ?? "", session.FlowVariablesJson);
                            }
                        }
                        break;

                    case "input":
                        // If we just landed on this node, we wait for the next turn.
                        // We check if we ALREADY waited for input. We can do this by using a transient flag
                        // or just checking if userMessage is provided and we are already AT this node.
                        // Actually, when we transition TO an input node, we should yield and wait.
                        // But wait! ExecuteFlowStepAsync is called WHEN the user sends a message.
                        // So if we are currently AT an input node, it means the user just provided the input.
                        // We should capture it, and move to the next node.
                        // Wait, how do we distinguish "just arrived" vs "user replied"?
                        // Let's use a simple state: when we move to an "input" node, we yield break.
                        // The next time the user types, they are at the "input" node, so we process it and move on.
                        // To achieve this: if userMessage is NOT empty and this is the FIRST node evaluated in this call,
                        // we treat it as answering the input.
                        if (safetyCounter == 1 && !string.IsNullOrWhiteSpace(userMessage))
                        {
                            // User provided input. Save variable if specified.
                            var inputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                            if (inputData != null && inputData.TryGetValue("config", out var inputConfig) && inputConfig.ValueKind == JsonValueKind.Object)
                            {
                                if (inputConfig.TryGetProperty("variableName", out var varNameProp))
                                {
                                    var varName = varNameProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(varName))
                                    {
                                        SaveVariable(session, varName, userMessage);
                                    }
                                }
                            }
                            // Move to the next node.
                        }
                        else
                        {
                            // Just arrived at this node. We must wait for user input.
                            shouldWait = true;
                        }
                        break;

                    case "ai":
                        var aiData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                        string prompt = "Respond to the user.";
                        if (aiData != null && aiData.TryGetValue("config", out var aiConfig) && aiConfig.ValueKind == JsonValueKind.Object)
                        {
                            if (aiConfig.TryGetProperty("prompt", out var promptProp))
                            {
                                prompt = InterpolateVariables(promptProp.GetString() ?? prompt, session.FlowVariablesJson);
                            }
                        }
                        
                        // Combine the prompt with the user's message
                        var fullPrompt = $"{prompt}\nUser Input: {userMessage}";
                        
                        var aiResponse = "";
                        var provider = _llmFactory.GetProvider(project.Provider, null, session.Configuration);
                        var apiKey = _llmFactory.ResolveApiKey(project.Provider, session.Configuration);
                        
                        var msgs = new List<GenericChatMessage> { new GenericChatMessage { Role = "user", Content = fullPrompt } };
                        await foreach (var chunk in provider.StreamGenerateContentAsync(msgs, null, null, project.ModelName, apiKey))
                        {
                            if (chunk.Text != null)
                            {
                                aiResponse += chunk.Text;
                                yield return chunk.Text;
                            }
                        }
                        responseText = null; // Already streamed
                        break;

                    case "webhook":
                        var webhookData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                        if (webhookData != null && webhookData.TryGetValue("config", out var whConfig) && whConfig.ValueKind == JsonValueKind.Object)
                        {
                            if (whConfig.TryGetProperty("url", out var urlProp))
                            {
                                var url = InterpolateVariables(urlProp.GetString() ?? "", session.FlowVariablesJson);
                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    try
                                    {
                                        using var client = new HttpClient();
                                        // Include variables in payload
                                        var variables = string.IsNullOrEmpty(session.FlowVariablesJson) 
                                            ? new Dictionary<string, string>() 
                                            : JsonSerializer.Deserialize<Dictionary<string, string>>(session.FlowVariablesJson);
                                            
                                        var payload = new { sessionId = session.Id, userMessage, variables };
                                        var res = await client.PostAsJsonAsync(url, payload);
                                        if (res.IsSuccessStatusCode)
                                        {
                                            // Could capture response data if needed
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Webhook node failed: {Url}", url);
                                    }
                                }
                            }
                        }
                        break;
                }

                if (responseText != null)
                {
                    yield return responseText;
                }

                if (shouldWait)
                {
                    break;
                }

                // Find next node via edges
                var edges = await _db.FlowEdges
                    .Where(e => e.SourceNodeId == currentNode.Id)
                    .ToListAsync();

                if (edges.Count == 0)
                {
                    // Flow ends
                    session.ActiveFlowId = null;
                    session.CurrentNodeId = null;
                    await _db.SaveChangesAsync();
                    break;
                }
                else
                {
                    var nextEdge = edges.FirstOrDefault(e => EvaluateCondition(e.Condition, userMessage)) 
                                   ?? edges.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Condition)) 
                                   ?? edges.First();
                                   
                    session.CurrentNodeId = nextEdge.TargetNodeId;
                    await _db.SaveChangesAsync();
                }
            }
        }

        private string InterpolateVariables(string text, string? variablesJson)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(variablesJson))
                return text;

            var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(variablesJson);
            if (variables == null) return text;

            foreach (var kvp in variables)
            {
                text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
            }
            return text;
        }

        private void SaveVariable(ChatSession session, string key, string value)
        {
            var variables = string.IsNullOrWhiteSpace(session.FlowVariablesJson)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(session.FlowVariablesJson) ?? new Dictionary<string, string>();

            variables[key] = value;
            session.FlowVariablesJson = JsonSerializer.Serialize(variables);
        }

        private bool EvaluateCondition(string? conditionStr, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(conditionStr)) return false;

            var parts = conditionStr.Split(':', 2);
            if (parts.Length != 2) return false;

            var op = parts[0].Trim().ToLowerInvariant();
            var val = parts[1].Trim().ToLowerInvariant();
            var input = userMessage.Trim().ToLowerInvariant();

            return op switch
            {
                "equals" => input == val,
                "contains" => input.Contains(val),
                "regex" => Regex.IsMatch(userMessage, parts[1].Trim(), RegexOptions.IgnoreCase),
                _ => false
            };
        }
    }
}
