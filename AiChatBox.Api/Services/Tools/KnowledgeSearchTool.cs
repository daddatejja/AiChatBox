using System.Text;
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
                
                // 1. Get embedding for the semantic query
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(query, apiKeyOverride: _geminiApiKey, projectId: _projectId, userId: userId);

                // 2. Fetch top 20 semantic matches
                var semanticChunks = await db.DocumentChunks
                    .Include(c => c.Document)
                    .Where(c => c.Document!.ProjectId == _projectId)
                    .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
                    .Take(20)
                    .Select(c => new { c.Id, c.Content, c.ChunkIndex, FileName = c.Document!.FileName })
                    .ToListAsync();

                // 3. Fetch top 20 text-based keyword matches (case-insensitive keyword matching)
                var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5).ToList();
                var textChunks = new List<dynamic>();
                
                if (terms.Count > 0)
                {
                    var queryable = db.DocumentChunks
                        .Include(c => c.Document)
                        .Where(c => c.Document!.ProjectId == _projectId);

                    // EF Core case-insensitive sub-string queries for each term
                    var matches = await queryable
                        .Where(c => terms.Any(term => c.Content.ToLower().Contains(term.ToLower())))
                        .Take(20)
                        .Select(c => new { c.Id, c.Content, c.ChunkIndex, FileName = c.Document!.FileName })
                        .ToListAsync();

                    textChunks.AddRange(matches.Cast<dynamic>());
                }

                // 4. Perform Reciprocal Rank Fusion (RRF)
                var rrfScores = new Dictionary<Guid, (string Content, int ChunkIndex, string FileName, double Score)>();

                // Vector rankings (weight: 1 / (60 + rank))
                for (int i = 0; i < semanticChunks.Count; i++)
                {
                    var chunk = semanticChunks[i];
                    var rank = i + 1;
                    var score = 1.0 / (60.0 + rank);

                    if (rrfScores.TryGetValue(chunk.Id, out var existing))
                        rrfScores[chunk.Id] = (chunk.Content, chunk.ChunkIndex, chunk.FileName, existing.Score + score);
                    else
                        rrfScores[chunk.Id] = (chunk.Content, chunk.ChunkIndex, chunk.FileName, score);
                }

                // Text rankings
                for (int i = 0; i < textChunks.Count; i++)
                {
                    var chunk = textChunks[i];
                    var rank = i + 1;
                    var score = 1.0 / (60.0 + rank);

                    Guid chunkId = chunk.Id;
                    string content = chunk.Content;
                    int index = chunk.ChunkIndex;
                    string fileName = chunk.FileName;

                    if (rrfScores.TryGetValue(chunkId, out var existing))
                        rrfScores[chunkId] = (content, index, fileName, existing.Score + score);
                    else
                        rrfScores[chunkId] = (content, index, fileName, score);
                }

                // Sort by unified RRF fusion score descending and take the top 5
                var finalChunks = rrfScores.Values
                    .OrderByDescending(x => x.Score)
                    .Take(5)
                    .ToList();

                if (finalChunks.Count == 0)
                    return new ToolResult { ToolName = Name, Content = "No relevant information found in the knowledge base." };

                // 5. Format results with structured source headers for clear LLM citations
                var sb = new StringBuilder();
                sb.AppendLine("Information found in knowledge base (RRF Rank-Fused):");
                foreach (var chunk in finalChunks)
                {
                    sb.AppendLine($"\n--- [SOURCE: {chunk.FileName}, CHUNK INDEX: {chunk.ChunkIndex}] ---");
                    sb.AppendLine(chunk.Content);
                }

                return new ToolResult { ToolName = Name, Content = sb.ToString() };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = Name, Error = $"Failed to search knowledge base: {ex.Message}" };
            }
        }
    }
}
