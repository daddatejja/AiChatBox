using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using System.Text.RegularExpressions;
using Npgsql;

namespace AiChatBox.Api.Services.Tools
{
    public class UserSqlTool(ProjectDatabase dbConfig, string decryptedConnectionString) : ITool
    {
        public string Name => "query_project_database";
        public string Description => "Executes read-only SQL queries on the user's project database. Use this to fetch data, generate reports, or answer analytical questions based on the provided schema.";

        public JsonObject ParametersSchema => new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "The SQL SELECT query to execute. Only SELECT is allowed."
                },
                ["format_instruction"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional instruction on how the data should be formatted (e.g., 'Return as a list of top 5', 'Group by month for chart')."
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
                
                if (!lower.StartsWith("select")) {
                    return new ToolResult { ToolName = Name, Error = "Only SELECT queries are permitted for security reasons." };
                }

                // Block destructive keywords (whole words only)
                string[] blocked = ["delete", "update", "insert", "drop", "truncate", "alter", "create", "grant", "revoke", "into"];
                foreach (var word in blocked) {
                    if (Regex.IsMatch(lower, $@"\b{word}\b")) {
                         return new ToolResult { ToolName = Name, Error = $"Destructive or administrative keyword '{word}' is not permitted." };
                    }
                }

                using IDbConnection conn = dbConfig.Type switch
                {
                    DatabaseType.PostgreSQL => new NpgsqlConnection(decryptedConnectionString),
                    DatabaseType.MySQL => new MySqlConnection(decryptedConnectionString),
                    DatabaseType.SQLServer => new SqlConnection(decryptedConnectionString),
                    DatabaseType.SQLite => new Microsoft.Data.Sqlite.SqliteConnection(decryptedConnectionString),
                    _ => throw new NotSupportedException($"Database type {dbConfig.Type} is not supported.")
                };

                var results = await conn.QueryAsync(query);

                return new ToolResult 
                { 
                    ToolName = Name, 
                    Content = new 
                    { 
                        data = results,
                        sql = query,
                        rowCount = results.Count()
                    } 
                };
            }
            catch (Exception ex)
            {
                return new ToolResult { ToolName = Name, Error = ex.Message };
            }
        }
    }
}
