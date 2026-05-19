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
                .Where(f => f.ProjectId == session.ProjectId && f.IsActive)
                .Include(f => f.Nodes)
                .ToListAsync();

            var normalizedMsg = userMessage.Trim().ToLowerInvariant();

            foreach (var flow in flows)
            {
                var triggerNode = flow.Nodes.FirstOrDefault(n => n.Type.Equals("trigger", StringComparison.OrdinalIgnoreCase));
                if (triggerNode == null) continue;

                // Let's parse DataJson config for custom triggerType
                string triggerType = "keyword";
                string matchVal = flow.TriggerKeyword ?? "";

                try
                {
                    var nodeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(triggerNode.DataJson);
                    if (nodeData != null && nodeData.TryGetValue("config", out var configElement) && configElement.ValueKind == JsonValueKind.Object)
                    {
                        if (configElement.TryGetProperty("triggerType", out var typeProp))
                        {
                            triggerType = typeProp.GetString() ?? "keyword";
                        }
                        if (configElement.TryGetProperty("keyword", out var keyProp))
                        {
                            matchVal = keyProp.GetString() ?? matchVal;
                        }
                        else if (configElement.TryGetProperty("command", out var cmdProp))
                        {
                            matchVal = cmdProp.GetString() ?? matchVal;
                        }
                    }
                }
                catch { }

                triggerType = triggerType.ToLowerInvariant();
                matchVal = matchVal.Trim().ToLowerInvariant();

                bool isMatch = false;
                if (triggerType == "command")
                {
                    if (!string.IsNullOrEmpty(matchVal))
                    {
                        isMatch = normalizedMsg.Equals(matchVal) || normalizedMsg.StartsWith(matchVal + " ");
                    }
                }
                else if (triggerType == "keyword" || string.IsNullOrEmpty(triggerType))
                {
                    if (!string.IsNullOrEmpty(matchVal))
                    {
                        isMatch = normalizedMsg.Contains(matchVal);
                    }
                    else if (!string.IsNullOrEmpty(flow.TriggerKeyword))
                    {
                        isMatch = normalizedMsg.Contains(flow.TriggerKeyword.ToLowerInvariant());
                    }
                }

                if (isMatch)
                {
                    session.ActiveFlowId = flow.Id;
                    session.CurrentNodeId = triggerNode.Id;
                    await _db.SaveChangesAsync();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if there is an onStart flow configured for the project and triggers it.
        /// </summary>
        public async Task<bool> TryTriggerOnStartFlowAsync(ChatSession session)
        {
            if (session.ProjectId == null)
                return false;

            var flows = await _db.ConversationFlows
                .Where(f => f.ProjectId == session.ProjectId && f.IsActive)
                .Include(f => f.Nodes)
                .ToListAsync();

            foreach (var flow in flows)
            {
                var triggerNode = flow.Nodes.FirstOrDefault(n => n.Type.Equals("trigger", StringComparison.OrdinalIgnoreCase));
                if (triggerNode == null) continue;

                try
                {
                    var nodeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(triggerNode.DataJson);
                    if (nodeData != null && nodeData.TryGetValue("config", out var configElement) && configElement.ValueKind == JsonValueKind.Object)
                    {
                        if (configElement.TryGetProperty("triggerType", out var typeProp))
                        {
                            var triggerType = typeProp.GetString()?.ToLowerInvariant();
                            if (triggerType == "onstart")
                            {
                                session.ActiveFlowId = flow.Id;
                                session.CurrentNodeId = triggerNode.Id;
                                await _db.SaveChangesAsync();
                                return true;
                            }
                        }
                    }
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// Manually triggers a specific flow by its ID for the session.
        /// </summary>
        public async Task<bool> TriggerFlowByIdAsync(ChatSession session, Guid flowId)
        {
            var flow = await _db.ConversationFlows.FirstOrDefaultAsync(f => f.Id == flowId && f.IsActive);
            if (flow == null) return false;

            var triggerNode = await _db.FlowNodes
                .FirstOrDefaultAsync(n => n.FlowId == flow.Id && n.Type == "trigger");

            if (triggerNode != null)
            {
                session.ActiveFlowId = flow.Id;
                session.CurrentNodeId = triggerNode.Id;
                await _db.SaveChangesAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Executes the current step of the active flow.
        /// Yields stream chunks back to the user, or nothing if the flow is yielding/waiting.
        /// If the flow ends, it clears ActiveFlowId.
        /// </summary>
        public async IAsyncEnumerable<ChatStreamChunk> ExecuteFlowStepAsync(ChatSession session, string userMessage, Project project)
        {
            if (session.ActiveFlowId == null || session.CurrentNodeId == null)
                yield break;

            if (session.Configuration == null && session.ConfigurationId.HasValue)
            {
                session.Configuration = await _db.Configurations.FirstOrDefaultAsync(c => c.Id == session.ConfigurationId.Value);
            }
            if (session.Configuration == null && project != null)
            {
                session.Configuration = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Name == "Default")
                                        ?? await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == project.Id);
            }

            FlowExecutionLog? executionLog = null;
            if (session.ActiveFlowId != null)
            {
                executionLog = await _db.FlowExecutionLogs
                    .FirstOrDefaultAsync(l => l.SessionId == session.Id && l.FlowId == session.ActiveFlowId && l.CompletedAt == null);
                if (executionLog == null)
                {
                    executionLog = new FlowExecutionLog
                    {
                        Id = Guid.NewGuid(),
                        FlowId = session.ActiveFlowId.Value,
                        SessionId = session.Id,
                        StartedAt = DateTime.UtcNow,
                        StepsJson = "[]"
                    };
                    _db.FlowExecutionLogs.Add(executionLog);
                    await _db.SaveChangesAsync();
                }
            }

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
                ChatStreamChunk? chunkResponse = null;
                bool shouldWait = false;
                string? telemetryOutput = null;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
                                var text = InterpolateVariables(textProp.GetString() ?? "", session.FlowVariablesJson);
                                chunkResponse = new ChatStreamChunk { Text = text, SessionId = session.Id };
                                telemetryOutput = text;
                            }
                        }
                        break;

                    case "input":
                        {
                            var inputData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                            if (safetyCounter == 1 && !string.IsNullOrWhiteSpace(userMessage))
                            {
                                // User provided input. Save variable if specified.
                                if (inputData != null && inputData.TryGetValue("config", out var inputConfig) && inputConfig.ValueKind == JsonValueKind.Object)
                                {
                                    if (inputConfig.TryGetProperty("variableName", out var varNameProp))
                                    {
                                        var varName = varNameProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(varName))
                                        {
                                            SaveVariable(session, varName, userMessage);
                                            telemetryOutput = userMessage;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Yield inline prompt if configured on arrival
                                if (inputData != null && inputData.TryGetValue("config", out var inputConfig) && inputConfig.ValueKind == JsonValueKind.Object)
                                {
                                    if (inputConfig.TryGetProperty("promptText", out var promptProp))
                                    {
                                        var promptText = promptProp.GetString();
                                        if (!string.IsNullOrWhiteSpace(promptText))
                                        {
                                            var interpolated = InterpolateVariables(promptText, session.FlowVariablesJson);
                                            chunkResponse = new ChatStreamChunk { Text = interpolated, SessionId = session.Id };
                                            telemetryOutput = interpolated;
                                        }
                                    }
                                }
                                shouldWait = true;
                            }
                        }
                        break;

                    case "ai":
                        {
                            var aiData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                            string prompt = "Respond to the user.";
                            bool runInBg = false;
                            string storeVarName = string.Empty;

                            if (aiData != null && aiData.TryGetValue("config", out var aiConfig) && aiConfig.ValueKind == JsonValueKind.Object)
                            {
                                if (aiConfig.TryGetProperty("prompt", out var promptProp))
                                {
                                    prompt = InterpolateVariables(promptProp.GetString() ?? prompt, session.FlowVariablesJson);
                                }
                                if (aiConfig.TryGetProperty("runInBackground", out var bgProp))
                                {
                                    runInBg = bgProp.ValueKind == JsonValueKind.True || (bgProp.ValueKind != JsonValueKind.False && bgProp.GetBoolean());
                                }
                                if (aiConfig.TryGetProperty("storeVariableName", out var storeProp))
                                {
                                    storeVarName = storeProp.GetString() ?? string.Empty;
                                }
                            }
                            
                            var finalAiResult = "";
                            var aiResponseBuilder = new System.Text.StringBuilder();
                            bool initFailed = false;
                            string initErrorMessage = "";
                            IAsyncEnumerator<LlmResponseChunk>? enumerator = null;
                            
                            try
                            {
                                var provider = _llmFactory.GetProvider(project.Provider, null, session.Configuration);
                                var apiKey = _llmFactory.ResolveApiKey(project.Provider, session.Configuration);
                                
                                var systemPrompt = prompt;
                                var userContent = !string.IsNullOrWhiteSpace(userMessage) ? userMessage : "Process request";
                                var msgs = new List<GenericChatMessage> { new GenericChatMessage { Role = "user", Content = userContent } };
                                
                                enumerator = provider.StreamGenerateContentAsync(msgs, systemPrompt, null, project.ModelName, apiKey).GetAsyncEnumerator();
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "AI node initialization failed in flow {FlowId}, node {NodeId}", session.ActiveFlowId, session.CurrentNodeId);
                                initFailed = true;
                                initErrorMessage = ex.Message;
                            }

                            if (initFailed)
                            {
                                finalAiResult = $"Error: AI Initialization Failed - {initErrorMessage}";
                                if (!runInBg)
                                {
                                    yield return new ChatStreamChunk { Text = $"I'm sorry, I encountered an issue processing this request: {initErrorMessage}", SessionId = session.Id };
                                }
                            }
                            else if (enumerator != null)
                            {
                                try
                                {
                                    bool hasMore = true;
                                    while (hasMore)
                                    {
                                        LlmResponseChunk? chunk = null;
                                        try
                                        {
                                            if (await enumerator.MoveNextAsync())
                                            {
                                                chunk = enumerator.Current;
                                            }
                                            else
                                            {
                                                hasMore = false;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, "AI stream failed during iteration in flow {FlowId}, node {NodeId}", session.ActiveFlowId, session.CurrentNodeId);
                                            finalAiResult = $"Error: AI Stream Failed - {ex.Message}";
                                            hasMore = false;
                                        }

                                        if (chunk != null)
                                        {
                                            if (chunk.Text != null)
                                            {
                                                aiResponseBuilder.Append(chunk.Text);
                                                if (!runInBg)
                                                {
                                                    yield return new ChatStreamChunk { Text = chunk.Text, SessionId = session.Id };
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    await enumerator.DisposeAsync();
                                }

                                if (string.IsNullOrEmpty(finalAiResult))
                                {
                                    finalAiResult = aiResponseBuilder.ToString();
                                }
                            }
                            
                            telemetryOutput = finalAiResult;
                            
                            if (runInBg && !string.IsNullOrWhiteSpace(storeVarName))
                            {
                                SaveVariable(session, storeVarName, finalAiResult);
                            }
                        }
                        break;

                    case "richresponse":
                        var richData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                        if (richData != null && richData.TryGetValue("config", out var richConfig) && richConfig.ValueKind == JsonValueKind.Object)
                        {
                            var responseType = richConfig.TryGetProperty("responseType", out var typeProp) ? typeProp.GetString() : "card";
                            responseType = responseType?.ToLowerInvariant() ?? "card";
                            
                            var payloadDict = new Dictionary<string, object>();
                            
                            if (responseType == "card")
                            {
                                payloadDict["title"] = InterpolateVariables(richConfig.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                payloadDict["body"] = InterpolateVariables(richConfig.TryGetProperty("body", out var bProp) ? bProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                payloadDict["imageUrl"] = InterpolateVariables(richConfig.TryGetProperty("imageUrl", out var imgProp) ? imgProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                payloadDict["buttonLabel"] = InterpolateVariables(richConfig.TryGetProperty("buttonLabel", out var blProp) ? blProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                payloadDict["buttonUrl"] = InterpolateVariables(richConfig.TryGetProperty("buttonUrl", out var buProp) ? buProp.GetString() ?? "" : "", session.FlowVariablesJson);
                            }
                            else if (responseType == "redirect")
                            {
                                payloadDict["url"] = InterpolateVariables(richConfig.TryGetProperty("url", out var uProp) ? uProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                payloadDict["seconds"] = richConfig.TryGetProperty("seconds", out var secProp) && secProp.ValueKind == JsonValueKind.Number ? secProp.GetInt32() : 5;
                                payloadDict["countdownText"] = InterpolateVariables(richConfig.TryGetProperty("countdownText", out var ctProp) ? ctProp.GetString() ?? "" : "", session.FlowVariablesJson);
                            }
                            else if (responseType == "file")
                            {
                                payloadDict["fileUrl"] = InterpolateVariables(richConfig.TryGetProperty("fileUrl", out var fuProp) ? fuProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                payloadDict["fileName"] = InterpolateVariables(richConfig.TryGetProperty("fileName", out var fnProp) ? fnProp.GetString() ?? "" : "", session.FlowVariablesJson);
                            }
                            else if (responseType == "buttons")
                            {
                                var buttonsList = new List<Dictionary<string, string>>();
                                if (richConfig.TryGetProperty("buttons", out var buttonsProp) && buttonsProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var buttonEl in buttonsProp.EnumerateArray())
                                    {
                                        var buttonDict = new Dictionary<string, string>();
                                        if (buttonEl.TryGetProperty("label", out var bLabel)) buttonDict["label"] = InterpolateVariables(bLabel.GetString() ?? "", session.FlowVariablesJson);
                                        if (buttonEl.TryGetProperty("action", out var bAction)) buttonDict["action"] = bAction.GetString() ?? "next";
                                        if (buttonEl.TryGetProperty("value", out var bVal)) buttonDict["value"] = InterpolateVariables(bVal.GetString() ?? "", session.FlowVariablesJson);
                                        buttonsList.Add(buttonDict);
                                    }
                                }
                                payloadDict["buttons"] = buttonsList;
                            }
                            else if (responseType == "form")
                            {
                                payloadDict["title"] = InterpolateVariables(richConfig.TryGetProperty("title", out var formTProp) ? formTProp.GetString() ?? "" : "", session.FlowVariablesJson);
                                
                                var fieldsList = new List<Dictionary<string, object>>();
                                if (richConfig.TryGetProperty("fields", out var fieldsProp) && fieldsProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var fieldEl in fieldsProp.EnumerateArray())
                                    {
                                        var fieldDict = new Dictionary<string, object>();
                                        if (fieldEl.TryGetProperty("label", out var fLabel)) fieldDict["label"] = InterpolateVariables(fLabel.GetString() ?? "", session.FlowVariablesJson);
                                        if (fieldEl.TryGetProperty("name", out var fName)) fieldDict["name"] = fName.GetString() ?? "";
                                        if (fieldEl.TryGetProperty("type", out var fType)) fieldDict["type"] = fType.GetString() ?? "text";
                                        if (fieldEl.TryGetProperty("required", out var fReq)) fieldDict["required"] = fReq.GetBoolean();
                                        if (fieldEl.TryGetProperty("placeholder", out var fPlac)) fieldDict["placeholder"] = InterpolateVariables(fPlac.GetString() ?? "", session.FlowVariablesJson);
                                        
                                        if (fieldEl.TryGetProperty("options", out var fOpts))
                                        {
                                            if (fOpts.ValueKind == JsonValueKind.Array)
                                            {
                                                var optList = new List<string>();
                                                foreach (var o in fOpts.EnumerateArray()) optList.Add(o.GetString() ?? "");
                                                fieldDict["options"] = optList;
                                            }
                                            else if (fOpts.ValueKind == JsonValueKind.String)
                                            {
                                                fieldDict["options"] = fOpts.GetString() ?? "";
                                            }
                                        }
                                        fieldsList.Add(fieldDict);
                                    }
                                }
                                payloadDict["fields"] = fieldsList;
                            }
                            
                            var payloadJson = JsonSerializer.Serialize(payloadDict);
                            chunkResponse = new ChatStreamChunk
                            {
                                SessionId = session.Id,
                                RuleResponse = new RuleResponseChunk
                                {
                                    ResponseType = responseType,
                                    Payload = payloadJson
                                }
                            };
                            telemetryOutput = payloadJson;
                        }
                        break;

                    case "condition":
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
                                        var variables = string.IsNullOrEmpty(session.FlowVariablesJson) 
                                            ? new Dictionary<string, string>() 
                                            : JsonSerializer.Deserialize<Dictionary<string, string>>(session.FlowVariablesJson);
                                            
                                        var payload = new { sessionId = session.Id, userMessage, variables };
                                        var res = await client.PostAsJsonAsync(url, payload);
                                        telemetryOutput = $"Webhook triggered. Status: {res.StatusCode}";
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Webhook node failed: {Url}", url);
                                        telemetryOutput = $"Webhook failed: {ex.Message}";
                                    }
                                }
                            }
                        }
                        break;
                }

                stopwatch.Stop();
                var durationMs = stopwatch.Elapsed.TotalMilliseconds;

                if (chunkResponse != null)
                {
                    yield return chunkResponse;
                }

                if (executionLog != null)
                {
                    string nodeLabel = currentNode.Type;
                    try
                    {
                        var nodeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentNode.DataJson);
                        if (nodeData != null && nodeData.TryGetValue("config", out var configEl))
                        {
                            if (configEl.TryGetProperty("label", out var labelProp) && labelProp.ValueKind == JsonValueKind.String)
                            {
                                nodeLabel = labelProp.GetString() ?? nodeLabel;
                            }
                            else if (configEl.TryGetProperty("name", out var labelProp2) && labelProp2.ValueKind == JsonValueKind.String)
                            {
                                nodeLabel = labelProp2.GetString() ?? nodeLabel;
                            }
                        }
                    }
                    catch {}

                    List<FlowStepTelemetry> steps = new();
                    try
                    {
                        steps = JsonSerializer.Deserialize<List<FlowStepTelemetry>>(executionLog.StepsJson) ?? new();
                    }
                    catch {}

                    steps.Add(new FlowStepTelemetry
                    {
                        NodeId = currentNode.Id,
                        NodeType = currentNode.Type,
                        NodeLabel = nodeLabel,
                        VariablesSnapshotJson = session.FlowVariablesJson ?? "{}",
                        ExecutedAt = DateTime.UtcNow,
                        DurationMs = durationMs,
                        InputMessage = currentNode.Type.Equals("input", StringComparison.OrdinalIgnoreCase) ? userMessage : null,
                        OutputMessage = telemetryOutput ?? chunkResponse?.Text ?? (chunkResponse?.RuleResponse != null ? JsonSerializer.Serialize(chunkResponse.RuleResponse) : null)
                    });

                    executionLog.StepsJson = JsonSerializer.Serialize(steps);
                    await _db.SaveChangesAsync();
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
                    session.ActiveFlowId = null;
                    session.CurrentNodeId = null;
                    await _db.SaveChangesAsync();

                    if (executionLog != null)
                    {
                        executionLog.CompletedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }
                    break;
                }
                else
                {
                    FlowEdge? nextEdge = null;
                    if (currentNode.Type.ToLowerInvariant() == "condition")
                    {
                        bool evalResult = EvaluateConditionNode(currentNode, session);
                        var targetCond = evalResult ? "true" : "false";
                        nextEdge = edges.FirstOrDefault(e => e.Condition != null && e.Condition.Trim().ToLowerInvariant() == targetCond)
                                   ?? edges.FirstOrDefault();
                    }
                    else if (currentNode.Type.ToLowerInvariant() == "switch")
                    {
                        nextEdge = EvaluateSwitchNode(currentNode, session, edges);
                    }
                    else
                    {
                        nextEdge = edges.FirstOrDefault(e => EvaluateCondition(e.Condition, userMessage)) 
                                       ?? edges.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Condition)) 
                                       ?? edges.First();
                    }
                                   
                    session.CurrentNodeId = nextEdge.TargetNodeId;
                    await _db.SaveChangesAsync();
                }
            }
        }

        private FlowEdge EvaluateSwitchNode(FlowNode node, ChatSession session, List<FlowEdge> edges)
        {
            try
            {
                var nodeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(node.DataJson);
                if (nodeData == null || !nodeData.TryGetValue("config", out var configElement) || configElement.ValueKind != JsonValueKind.Object)
                    return edges.FirstOrDefault() ?? throw new InvalidOperationException("No outgoing edges for switch node");

                if (!configElement.TryGetProperty("variableName", out var varNameProp))
                    return edges.FirstOrDefault() ?? throw new InvalidOperationException("No outgoing edges for switch node");

                var varName = varNameProp.GetString();
                if (string.IsNullOrWhiteSpace(varName))
                    return edges.FirstOrDefault() ?? throw new InvalidOperationException("No outgoing edges for switch node");

                var variables = string.IsNullOrWhiteSpace(session.FlowVariablesJson)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(session.FlowVariablesJson) ?? new Dictionary<string, string>();

                variables.TryGetValue(varName, out var varValue);
                varValue ??= string.Empty;
                varValue = varValue.Trim();

                FlowEdge? matchedEdge = null;
                FlowEdge? defaultEdge = null;

                foreach (var edge in edges)
                {
                    var cond = edge.Condition?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(cond) || cond.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        defaultEdge = edge;
                        continue;
                    }

                    var parts = cond.Split(':', 2);
                    var op = parts[0].Trim().ToLowerInvariant();
                    var val = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();

                    bool isMatch = false;
                    if (parts.Length > 1 && op == "equals")
                    {
                        isMatch = varValue.Equals(val, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (parts.Length > 1 && op == "contains")
                    {
                        isMatch = varValue.Contains(val, StringComparison.OrdinalIgnoreCase);
                    }
                    else if (parts.Length > 1 && op == "regex")
                    {
                        try
                        {
                            isMatch = Regex.IsMatch(varValue, val, RegexOptions.IgnoreCase);
                        }
                        catch {}
                    }
                    else
                    {
                        // Fallback to direct equals match if no separator colon is found or it's a simple string comparison
                        isMatch = varValue.Equals(cond, StringComparison.OrdinalIgnoreCase);
                    }

                    if (isMatch)
                    {
                        matchedEdge = edge;
                        break;
                    }
                }

                return matchedEdge ?? defaultEdge ?? edges.FirstOrDefault() ?? throw new InvalidOperationException("No outgoing edges for switch node");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing switch node evaluation");
                return edges.FirstOrDefault() ?? throw new InvalidOperationException("No outgoing edges for switch node");
            }
        }

        private bool EvaluateConditionNode(FlowNode node, ChatSession session)
        {
            try
            {
                var nodeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(node.DataJson);
                if (nodeData == null || !nodeData.TryGetValue("config", out var configElement) || configElement.ValueKind != JsonValueKind.Object)
                    return false;

                if (!configElement.TryGetProperty("variableName", out var varNameProp))
                    return false;

                var varName = varNameProp.GetString();
                if (string.IsNullOrWhiteSpace(varName)) return false;

                var variables = string.IsNullOrWhiteSpace(session.FlowVariablesJson)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(session.FlowVariablesJson) ?? new Dictionary<string, string>();

                variables.TryGetValue(varName, out var varValue);
                varValue ??= string.Empty;

                var op = configElement.TryGetProperty("operator", out var opProp) ? opProp.GetString() : "equals";
                var compValue = configElement.TryGetProperty("value", out var valProp) ? valProp.GetString() : string.Empty;

                op = op?.Trim().ToLowerInvariant();
                compValue ??= string.Empty;

                switch (op)
                {
                    case "equals":
                        return varValue.Trim().Equals(compValue.Trim(), StringComparison.OrdinalIgnoreCase);
                    case "contains":
                        return varValue.Contains(compValue, StringComparison.OrdinalIgnoreCase);
                    case "regex":
                        return Regex.IsMatch(varValue, compValue, RegexOptions.IgnoreCase);
                    case "exists":
                    case "notempty":
                        return !string.IsNullOrWhiteSpace(varValue);
                    case "greaterthan":
                        if (double.TryParse(varValue, out var d1) && double.TryParse(compValue, out var d2))
                            return d1 > d2;
                        return false;
                    case "lessthan":
                        if (double.TryParse(varValue, out var d1L) && double.TryParse(compValue, out var d2L))
                            return d1L < d2L;
                        return false;
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate condition node {NodeId}", node.Id);
                return false;
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

    public class FlowStepTelemetry
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public string NodeLabel { get; set; } = string.Empty;
        public string VariablesSnapshotJson { get; set; } = "{}";
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public double DurationMs { get; set; }
        public string? InputMessage { get; set; }
        public string? OutputMessage { get; set; }
    }
}
