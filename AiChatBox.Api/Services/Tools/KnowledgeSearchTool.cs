using System.Text.Json;
using System.Text.Json.Nodes;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using AiChatBox.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AiChatBox.Api.Services.Tools
{
    public class KnowledgeSearchTool(IDbContextFactory<ChatDbContext> dbFactory, IEmbeddingService embeddingService, Guid projectId, string? geminiApiKey = null) : ITool
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly IEmbeddingService _embeddingService = embeddingService;
        private readonly Guid _projectId = projectId;
        private readonly string? _geminiApiKey = geminiApiKey;

        public string Name => "search_knowledge_base";
        public string Description => "Search the project's knowledge base for relevant documents and information. Use this when the user asks about specific internal projects, protocols, or data not in your general knowledge.";
        
        public JsonObject ParametersSchema => JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query to find relevant information in the knowledge base."
                }
            },
            "required": ["query"]
        }
        """)!.AsObject();

        public async Task<ToolResult> ExecuteAsync(string argumentsJson, string userId)
        {
            try
            {
                var args = JsonDocument.Parse(argumentsJson).RootElement;
                var query = args.GetProperty("query").GetString() ?? "";

                if (string.IsNullOrWhiteSpace(query))
                    return new ToolResult { ToolName = Name, Content = "Error: Search query is empty." };

                await using var db = await _dbFactory.CreateDbContextAsync();
                
                // Get embedding for the search query
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, apiKeyOverride: _geminiApiKey, projectId: _projectId, userId: userId);

                // Find top 5 relevant chunks
                var relevantChunks = await db.DocumentChunks
                    .Where(c => c.Document!.ProjectId == _projectId)
                    .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
                    .Take(5)
                    .Select(c => c.Content)
                    .ToListAsync();

                if (relevantChunks.Count == 0)
                    return new ToolResult { ToolName = Name, Content = "No relevant information found in the knowledge base." };

                var resultText = "Information found in knowledge base:\n" + string.Join("\n---\n", relevantChunks);
                return new ToolResult { ToolName = Name, Content = resultText };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = Name, Error = $"Failed to search knowledge base: {ex.Message}" };
            }
        }
    }
}
