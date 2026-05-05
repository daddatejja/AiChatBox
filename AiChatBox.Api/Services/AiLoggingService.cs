using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class AiLoggingService(IDbContextFactory<ChatDbContext> dbFactory, ILogger<AiLoggingService> logger) : IAiLoggingService
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly ILogger<AiLoggingService> _logger = logger;

        public async Task LogRequestAsync(AiRequestLog log)
        {
            log.CreatedAt = DateTime.UtcNow;

            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                db.AiRequestLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist AI request log to database");
            }

            _logger.LogInformation(
                "AI Request | Endpoint={Endpoint} User={UserId} Session={SessionId} " +
                "InputTokens={InputTokens} OutputTokens={OutputTokens} Duration={DurationMs}ms Error={Error}",
                log.Endpoint,
                log.UserId,
                log.SessionId,
                log.InputTokens,
                log.OutputTokens,
                log.DurationMs,
                log.ErrorMessage ?? "none");
        }
    }
}
