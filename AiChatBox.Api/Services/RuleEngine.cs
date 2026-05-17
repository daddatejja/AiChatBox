using System.Text.RegularExpressions;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// Evaluates user messages against project-level conversation rules.
    /// If a rule matches, returns a static response — bypassing the LLM entirely.
    /// This enables zero-cost, instant replies for common queries like FAQs and greetings.
    /// </summary>
    public class RuleEngine(ChatDbContext db, ILogger<RuleEngine> logger)
    {
        private readonly ChatDbContext _db = db;
        private readonly ILogger<RuleEngine> _logger = logger;

        /// <summary>
        /// Attempts to match the user's message against active rules for the given project.
        /// Returns the matching rule's response, or null if no rule matched (i.e. fall through to LLM).
        /// </summary>
        public async Task<string?> TryMatchAsync(Guid projectId, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return null;

            var rules = await _db.ConversationRules
                .Where(r => r.ProjectId == projectId && r.IsActive)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync();

            if (rules.Count == 0)
                return null;

            var normalizedMessage = userMessage.Trim().ToLowerInvariant();

            foreach (var rule in rules)
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
                            "Rule matched: {RuleType} '{Trigger}' for project {ProjectId}",
                            rule.Type, rule.Trigger, projectId);
                        return rule.Response;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error evaluating rule {RuleId} ({RuleType}: '{Trigger}')",
                        rule.Id, rule.Type, rule.Trigger);
                }
            }

            return null;
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
}
