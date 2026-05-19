using System.Text.Json;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.Extensions.Configuration;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// Classifies user messages into predefined intents using a lightweight LLM call.
    /// Used by RuleEngine (for intent-type rules) and HandoffService (for escalation detection).
    /// Uses the project's cheapest available model to minimize cost (~200 tokens in, ~30 tokens out).
    /// </summary>
    public class IntentClassifierService(
        LlmProviderFactory llmFactory,
        EncryptionService encryption,
        IConfiguration configuration,
        ILogger<IntentClassifierService> logger)
    {
        private readonly LlmProviderFactory _llmFactory = llmFactory;
        private readonly EncryptionService _encryption = encryption;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<IntentClassifierService> _logger = logger;

        // Preferred classification models in order of preference (cheapest/fastest first)
        private static readonly (string Provider, string Model)[] _preferredClassificationModels =
        [
            ("gemini", "gemini-3.1-flash-lite-preview"),
            ("gemini", "gemini-1.5-flash"),
            ("groq", "llama-3.3-70b-versatile"),
            ("cerebras", "llama-3.3-70b"),
            ("openai", "gpt-4o-mini"),
        ];

        /// <summary>
        /// Classifies user intent against a set of defined intents and optional escalation criteria.
        /// Returns the best matching intent ID and confidence score.
        /// </summary>
        public async Task<ClassificationResult> ClassifyAsync(
            string userMessage,
            List<string>? recentMessages,
            List<IntentDefinition> intents,
            string? escalationCriteria,
            ProjectConfiguration config,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var prompt = BuildClassificationPrompt(userMessage, recentMessages, intents, escalationCriteria);

                // Resolve the cheapest available model for classification
                var (provider, model, apiKey) = ResolveClassificationModel(config);

                _logger.LogDebug(
                    "Classifying intent using {Provider}/{Model} — {IntentCount} intents, escalation={HasEscalation}",
                    provider, model, intents.Count, escalationCriteria != null);

                var llmProvider = _llmFactory.GetProvider(provider, apiKey, config);

                var messages = new List<GenericChatMessage>
                {
                    new() { Role = "user", Content = prompt }
                };

                var response = await llmProvider.GenerateContentAsync(
                    messages,
                    systemPrompt: "You are a precise intent classifier. You MUST respond with ONLY a valid JSON object, no markdown, no explanation.",
                    modelName: model,
                    apiKeyOverride: apiKey,
                    cancellationToken: cancellationToken);

                return ParseClassificationResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Intent classification failed, returning 'none'");
                return new ClassificationResult("none", 0.0, "Classification failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Simplified check for just escalation detection (no intent rules).
        /// Used by HandoffService when only escalation criteria are configured.
        /// </summary>
        public async Task<ClassificationResult> CheckEscalationAsync(
            string userMessage,
            List<string>? recentMessages,
            string escalationCriteria,
            ProjectConfiguration config,
            CancellationToken cancellationToken = default)
        {
            return await ClassifyAsync(
                userMessage,
                recentMessages,
                intents: [],
                escalationCriteria: escalationCriteria,
                config,
                cancellationToken);
        }

        private string BuildClassificationPrompt(
            string userMessage,
            List<string>? recentMessages,
            List<IntentDefinition> intents,
            string? escalationCriteria)
        {
            var parts = new List<string>();

            parts.Add("Classify the user's message into one of the following intents. Respond with ONLY a JSON object: {\"intent\": \"<intent_id>\", \"confidence\": <0.0-1.0>}");
            parts.Add("");
            parts.Add("Available intents:");

            foreach (var intent in intents)
            {
                parts.Add($"- \"{intent.Id}\": {intent.Description}");
            }

            if (!string.IsNullOrEmpty(escalationCriteria))
            {
                parts.Add($"- \"escalation\": {escalationCriteria}");
            }

            parts.Add("- \"none\": The message does not match any of the above intents");
            parts.Add("");

            if (recentMessages != null && recentMessages.Count > 0)
            {
                parts.Add("Recent conversation context:");
                foreach (var msg in recentMessages.TakeLast(3))
                {
                    parts.Add($"  - {msg}");
                }
                parts.Add("");
            }

            parts.Add($"User message: \"{userMessage}\"");

            return string.Join("\n", parts);
        }

        private ClassificationResult ParseClassificationResponse(string response)
        {
            try
            {
                var cleaned = response.Trim();

                // Robustly locate the outermost JSON object to handle markdown reasoning/bullet points preceding the JSON block
                var firstBrace = cleaned.IndexOf('{');
                var lastBrace = cleaned.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace > firstBrace)
                {
                    cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
                }
                else
                {
                    // Strip markdown code fences if present as fallback
                    if (cleaned.StartsWith("```"))
                    {
                        var firstNewline = cleaned.IndexOf('\n');
                        if (firstNewline > 0) cleaned = cleaned[(firstNewline + 1)..];
                        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
                        cleaned = cleaned.Trim();
                    }
                }

                using var doc = JsonDocument.Parse(cleaned);
                var root = doc.RootElement;

                var intent = root.TryGetProperty("intent", out var intentProp) 
                    ? intentProp.GetString() ?? "none" 
                    : "none";

                var confidence = root.TryGetProperty("confidence", out var confProp)
                    ? confProp.GetDouble()
                    : 0.0;

                var reasoning = root.TryGetProperty("reasoning", out var reasonProp)
                    ? reasonProp.GetString()
                    : null;

                return new ClassificationResult(intent, Math.Clamp(confidence, 0.0, 1.0), reasoning);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse classification response: {Response}", response);
                return new ClassificationResult("none", 0.0, $"Parse error: {response}");
            }
        }

        private (string Provider, string Model, string? ApiKey) ResolveClassificationModel(ProjectConfiguration config)
        {
            // Try preferred models in order (cheapest first)
            foreach (var (provider, model) in _preferredClassificationModels)
            {
                var key = _llmFactory.ResolveApiKey(provider, config);
                if (!string.IsNullOrEmpty(key))
                {
                    return (provider, model, key);
                }

                // Check if a global key is configured in appsettings.json for this provider
                var globalKey = provider.ToLowerInvariant() switch
                {
                    "gemini" => _configuration["Gemini:ApiKey"],
                    "grok" => _configuration["Grok:ApiKey"],
                    "openai" => _configuration["OpenAi:ApiKey"],
                    _ => null
                };

                if (!string.IsNullOrEmpty(globalKey))
                {
                    return (provider, model, globalKey);
                }
            }

            // Fallback: use the project's default provider/model
            var defaultKey = _llmFactory.ResolveApiKey(config.DefaultProvider, config);
            return (config.DefaultProvider, config.DefaultModel, defaultKey);
        }
    }

    /// <summary>
    /// Result of an intent classification.
    /// </summary>
    public record ClassificationResult(
        /// <summary>The matched intent ID (e.g., "pricing", "escalation", "none")</summary>
        string IntentId,
        /// <summary>Confidence score from 0.0 to 1.0</summary>
        double Confidence,
        /// <summary>Optional reasoning from the classifier</summary>
        string? Reasoning = null)
    {
        /// <summary>Returns true if the result indicates a successful match above the given threshold.</summary>
        public bool IsMatch(double threshold) => IntentId != "none" && Confidence >= threshold;
    }

    /// <summary>
    /// Defines an intent for the classifier to match against.
    /// </summary>
    public record IntentDefinition(string Id, string Description);
}
