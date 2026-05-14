using AiChatBox.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class LogPruningService(ILogger<LogPruningService> logger, IDbContextFactory<ChatDbContext> dbFactory)
    {
        private readonly ILogger<LogPruningService> _logger = logger;
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Log Pruning Job is starting.");
            using var context = await _dbFactory.CreateDbContextAsync();

            var configurations = await context.Configurations.ToListAsync();
            int totalDeleted = 0;

            foreach (var config in configurations)
            {
                // Time-Based Pruning (if enabled)
                if (config.LogRetentionDays > 0)
                {
                    var cutoffDate = DateTime.UtcNow.AddDays(-config.LogRetentionDays);
                    var deletedCount = await context.AiRequestLogs
                        .Where(l => (l.ConfigurationId == config.Id || l.ProjectId == config.ProjectId) && !l.IsPinned && l.CreatedAt < cutoffDate)
                        .ExecuteDeleteAsync();

                    if (deletedCount > 0)
                    {
                        _logger.LogInformation("Pruned {Count} old unpinned logs for Configuration {ConfigId}", deletedCount, config.Id);
                        totalDeleted += deletedCount;
                    }
                }

                // Session Count Pruning
                if (config.MaxSessionsPerProject > 0)
                {
                    var liveSessions = await context.ChatSessions
                        .Where(s => s.ConfigurationId == config.Id)
                        .OrderByDescending(s => s.CreatedAt)
                        .Select(s => s.Id)
                        .ToListAsync();

                    if (liveSessions.Count > config.MaxSessionsPerProject)
                    {
                        var sessionsToDelete = liveSessions.Skip(config.MaxSessionsPerProject).ToList();
                        var deletedSessionsCount = await context.ChatSessions
                            .Where(s => sessionsToDelete.Contains(s.Id))
                            .ExecuteDeleteAsync();
                        
                        _logger.LogInformation("Pruned {Count} old sessions for Configuration {ConfigId}", deletedSessionsCount, config.Id);
                    }
                }

                // Message Count Pruning per Session
                if (config.MaxLogsPerSession > 0)
                {
                    var sessionsInConfig = await context.ChatSessions
                        .Where(s => s.ConfigurationId == config.Id)
                        .Select(s => s.Id)
                        .ToListAsync();

                    foreach (var sessionId in sessionsInConfig)
                    {
                        var logsCount = await context.AiRequestLogs
                            .CountAsync(l => l.SessionId == sessionId && !l.IsPinned);

                        if (logsCount > config.MaxLogsPerSession)
                        {
                            var numToDelete = logsCount - config.MaxLogsPerSession;
                            var oldestUnpinnedLogs = await context.AiRequestLogs
                                .Where(l => l.SessionId == sessionId && !l.IsPinned)
                                .OrderBy(l => l.CreatedAt)
                                .Select(l => l.Id)
                                .Take(numToDelete)
                                .ToListAsync();

                            var deletedLogsCount = await context.AiRequestLogs
                                .Where(l => oldestUnpinnedLogs.Contains(l.Id))
                                .ExecuteDeleteAsync();

                            if (deletedLogsCount > 0)
                            {
                                _logger.LogInformation("Pruned {Count} old logs for Session {SessionId}", deletedLogsCount, sessionId);
                                totalDeleted += deletedLogsCount;
                            }
                        }
                    }
                }
            }
            
            if (totalDeleted > 0)
                _logger.LogInformation("Total pruned logs across all projects: {Total}", totalDeleted);
        }
    }
}
