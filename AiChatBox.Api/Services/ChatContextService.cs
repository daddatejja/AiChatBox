using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class ChatContextService(ChatDbContext db, ILogger<ChatContextService> logger) : IChatContextService
    {
        private readonly ChatDbContext _db = db;
        private readonly ILogger<ChatContextService> _logger = logger;

        public async Task<IList<ChatMessage>> GetContextMessagesAsync(
            Guid sessionId, int maxMessages = 20, int maxTokens = 4000)
        {
            var messages = await _db.ChatMessages
                .Include(m => m.AttachedFile)
                .Where(m => m.SessionId == sessionId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(maxMessages)
                .ToListAsync();

            var selected = new List<ChatMessage>();
            var totalTokens = 0;

            foreach (var message in messages)
            {
                // Inject file text if not already in content
                if (message.AttachedFile != null && !string.IsNullOrEmpty(message.AttachedFile.ExtractedText))
                {
                    if (!message.Content.Contains("[Attached File Content]"))
                    {
                        message.Content = $"{message.Content}\n\n[Attached File: {message.AttachedFile.OriginalFileName}]\n{message.AttachedFile.ExtractedText}";
                    }
                }

                var msgTokens = message.TokenCount > 0
                    ? message.TokenCount
                    : GeminiServerService.StaticEstimateTokenCount(message.Content);

                if (totalTokens + msgTokens > maxTokens && selected.Count > 0)
                    break;

                selected.Add(message);
                totalTokens += msgTokens;
            }

            selected.Reverse();

            _logger.LogDebug(
                "Built context for session {SessionId}: {MessageCount} messages, ~{TokenCount} tokens",
                sessionId, selected.Count, totalTokens);

            return selected;
        }

        public Task<string> BuildSystemPromptAsync(string userId)
        {
            var prompt = $@"You are a helpful AI assistant. 
                            Date: {DateTime.Now:yyyy-MM-dd HH:mm}.
                            Reply in the same language the user uses.";

            return Task.FromResult(prompt);
        }
    }
}
