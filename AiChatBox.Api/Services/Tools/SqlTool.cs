using System.Text.Json;
using System.Text.Json.Nodes;
using AiChatBox.Api.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AiChatBox.Api.Services.Tools
{
    public class SqlTool(IConfiguration config) : ITool
    {
        public string Name => "query_database";
        public string Description => "Executes a read-only SQL query to retrieve information about chats, files, and logs. Use this to answer questions about user activity.";

        public JsonObject ParametersSchema => new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The SQL SELECT query to execute. Only SELECT is allowed."
                }
            },
            ["required"] = new JsonArray { "query" }
        };

        public async Task<ToolResult> ExecuteAsync(string argumentsJson, string userId)
        {
            try
            {
                var doc = JsonDocument.Parse(argumentsJson);
                var query = doc.RootElement.GetProperty("query").GetString();

                if (string.IsNullOrWhiteSpace(query)) return new ToolResult { ToolName = Name, Error = "No query provided" };

                // Security: Basic check for read-only
                var lower = query.ToLower().Trim();
                
                // Must start with SELECT
                if (!lower.StartsWith("select")) {
                    return new ToolResult { ToolName = Name, Error = "Only SELECT queries are permitted for security reasons." };
                }

                // Block common destructive keywords even in comments or subqueries
                string[] blocked = ["delete", "update", "insert", "drop", "truncate", "alter", "create", "grant", "revoke"];
                foreach (var word in blocked) {
                    if (lower.Contains(word)) {
                         return new ToolResult { ToolName = Name, Error = $"Destructive or administrative keyword '{word}' is not permitted." };
                    }
                }

                using var conn = new SqliteConnection(config.GetConnectionString("DefaultConnection"));
                var results = await conn.QueryAsync(query);

                return new ToolResult { ToolName = Name, Data = results };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = Name, Error = ex.Message };
            }
        }
    }
}
