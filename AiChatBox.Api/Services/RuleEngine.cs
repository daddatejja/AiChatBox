using System.Text.RegularExpressions;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// Evaluates user messages against project-level conversation rules.
    /// If a rule matches, returns a static response — bypassing the LLM entirely.
    /// 
    /// Supports five rule types:
    /// - command: Message starts with a trigger char + command name (e.g. "/pricing")
    /// - keyword: All comma-separated keywords must appear in the message
    /// - exact: Message must match exactly (case-insensitive)
    /// - regex: Message must match a regex pattern
    /// - intent: LLM classifies user intent against a natural-language description
    /// 
    /// Processing order: command rules first (Phase 0, instant, zero-cost),
    /// then keyword/exact/regex (Phase 1, instant, zero-cost),
    /// then intent rules (Phase 2, requires a lightweight LLM call).
    /// </summary>
    public class RuleEngine(ChatDbContext db, IntentClassifierService classifier, ILogger<RuleEngine> logger)
    {
        private readonly ChatDbContext _db = db;
        private readonly IntentClassifierService _classifier = classifier;
        private readonly ILogger<RuleEngine> _logger = logger;

        /// <summary>
        /// Attempts to match the user's message against active rules for the given project.
        /// Returns the matching rule's response, or null if no rule matched (i.e. fall through to LLM).
        /// </summary>
        public async Task<RuleMatchResult?> TryMatchAsync(
            Guid projectId,
            string userMessage,
            ProjectConfiguration? config = null,
            List<string>? recentMessages = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return null;

            if (config == null)
            {
                config = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Name == "Default", cancellationToken);
                if (config == null)
                {
                    config = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == projectId, cancellationToken);
                }
            }

            var rules = await _db.ConversationRules
                .Where(r => r.ProjectId == projectId && r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

            if (rules.Count == 0)
                return null;

            var normalizedMessage = userMessage.Trim().ToLowerInvariant();

            // ─── Phase 0: Command rules (highest priority, zero cost) ───────────────
            var commandRules = rules.Where(r => r.Type.ToLowerInvariant() == "command").ToList();

            foreach (var rule in commandRules)
            {
                if (string.IsNullOrEmpty(rule.CommandName)) continue;

                var triggerChar = string.IsNullOrEmpty(rule.CommandTriggerChar) ? "/" : rule.CommandTriggerChar;
                var commandTrigger = (triggerChar + rule.CommandName).ToLowerInvariant();

                // Match: message is exactly the command, or command followed by a space + args
                if (normalizedMessage == commandTrigger ||
                    normalizedMessage.StartsWith(commandTrigger + " ", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Rule matched (command): '{Command}' for project {ProjectId}",
                        commandTrigger, projectId);
                    return new RuleMatchResult(rule.Response, "command", 1.0, rule.IntentLabel,
                        rule.ResponseType, rule.ResponsePayload);
                }
            }

            // ─── Phase 1: Fast-path — keyword/exact/regex rules (zero cost, instant) ───
            var fastRules = rules.Where(r => r.Type.ToLowerInvariant() is "keyword" or "exact" or "regex").ToList();

            foreach (var rule in fastRules)
            {
                try
                {
                    bool matched = rule.Type.ToLowerInvariant() switch
                    {
                        "exact" => MatchExact(normalizedMessage, rule.Trigger),
                        "keyword" => MatchKeyword(normalizedMessage, rule.Trigger),
                        "regex" => MatchRegex(userMessage, rule.Trigger),
                        _ => false
                    };

                    if (matched)
                    {
                        _logger.LogInformation(
                            "Rule matched (fast-path): {RuleType} '{Trigger}' for project {ProjectId}",
                            rule.Type, rule.Trigger, projectId);
                        return new RuleMatchResult(rule.Response, rule.Type, 1.0, rule.IntentLabel,
                            rule.ResponseType, rule.ResponsePayload);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error evaluating rule {RuleId} ({RuleType}: '{Trigger}')",
                        rule.Id, rule.Type, rule.Trigger);
                }
            }

            // ─── Phase 2: Intent classification (LLM call) ───────────────────────────
            var intentRules = rules.Where(r => r.Type.ToLowerInvariant() == "intent").ToList();

            if (intentRules.Count > 0 && config != null)
            {
                try
                {
                    var intents = intentRules.Select(r => new IntentDefinition(
                        r.IntentLabel ?? r.Id.ToString(),
                        r.Trigger
                    )).ToList();

                    var result = await _classifier.ClassifyAsync(
                        userMessage,
                        recentMessages,
                        intents,
                        escalationCriteria: null,
                        config,
                        cancellationToken);

                    if (result.IntentId != "none" && result.IntentId != "escalation")
                    {
                        // Find the matching rule by intent label or ID
                        var matchedRule = intentRules.FirstOrDefault(r =>
                            (r.IntentLabel ?? r.Id.ToString()).Equals(result.IntentId, StringComparison.OrdinalIgnoreCase));

                        if (matchedRule != null && result.Confidence >= matchedRule.ConfidenceThreshold)
                        {
                            _logger.LogInformation(
                                "Rule matched (intent): '{IntentLabel}' confidence={Confidence:F2} for project {ProjectId}",
                                result.IntentId, result.Confidence, projectId);
                            return new RuleMatchResult(matchedRule.Response, "intent", result.Confidence, result.IntentId,
                                matchedRule.ResponseType, matchedRule.ResponsePayload);
                        }
                        else if (matchedRule != null)
                        {
                            _logger.LogDebug(
                                "Intent '{IntentId}' matched but below threshold ({Confidence:F2} < {Threshold:F2})",
                                result.IntentId, result.Confidence, matchedRule.ConfidenceThreshold);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Intent classification failed for project {ProjectId}, falling through to LLM", projectId);
                }
            }

            return null;
        }

        /// <summary>
        /// Returns all active command rules for the given project, optionally filtered by trigger character.
        /// Used by the widget's autocomplete endpoint.
        /// </summary>
        public async Task<List<CommandInfo>> GetCommandsAsync(Guid projectId, string? triggerChar = null, CancellationToken cancellationToken = default)
        {
            var query = _db.ConversationRules
                .Where(r => r.ProjectId == projectId && r.IsActive && r.Type == "command" && r.CommandName != null);

            if (!string.IsNullOrEmpty(triggerChar))
                query = query.Where(r => r.CommandTriggerChar == triggerChar);

            return await query
                .OrderBy(r => r.CommandName)
                .Select(r => new CommandInfo(
                    r.CommandName!,
                    r.CommandTriggerChar ?? "/",
                    r.CommandDescription))
                .ToListAsync(cancellationToken);
        }

        /// <summary>Exact match (case-insensitive, trimmed).</summary>
        private static bool MatchExact(string normalizedMessage, string trigger)
        {
            return normalizedMessage == trigger.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Keyword match: the trigger can contain multiple keywords separated by commas.
        /// ALL keywords must be present in the message for a match.
        /// Example: trigger "pricing, plans" matches "What are your pricing plans?"
        /// </summary>
        private static bool MatchKeyword(string normalizedMessage, string trigger)
        {
            var keywords = trigger.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (keywords.Length == 0) return false;

            return keywords.All(kw => normalizedMessage.Contains(kw.ToLowerInvariant()));
        }

        /// <summary>Regex pattern match with a timeout to prevent ReDoS.</summary>
        private static bool MatchRegex(string originalMessage, string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
            return regex.IsMatch(originalMessage);
        }
    }

    /// <summary>
    /// Result of a rule match, including metadata about how the match was made.
    /// </summary>
    public record RuleMatchResult(
        /// <summary>The static text response to return to the user (used when ResponseType = "text").</summary>
        string Response,
        /// <summary>How the rule was matched: "command", "keyword", "exact", "regex", or "intent"</summary>
        string MatchType,
        /// <summary>Confidence score (1.0 for command/keyword/exact/regex, 0.0-1.0 for intent)</summary>
        double Confidence,
        /// <summary>The intent label if matched via intent classification</summary>
        string? IntentLabel = null,
        /// <summary>How to deliver the response: text | redirect | card | ai | file | form | tool_call</summary>
        string ResponseType = "text",
        /// <summary>Structured JSON payload for non-text response types</summary>
        string? ResponsePayload = null);

    /// <summary>
    /// Lightweight command info returned by the widget autocomplete endpoint.
    /// </summary>
    public record CommandInfo(
        string CommandName,
        string TriggerChar,
        string? Description);
}
