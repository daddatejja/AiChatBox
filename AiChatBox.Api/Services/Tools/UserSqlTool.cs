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
using System.Net;
using System.Net.Sockets;

namespace AiChatBox.Api.Services.Tools
{
    public class UserSqlTool : ITool
    {
        private readonly ProjectDatabase _dbConfig;
        private readonly string _decryptedConnectionString;
        private readonly Dictionary<string, string> _sessionContext = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool _isWidget;

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

        public UserSqlTool(ProjectDatabase dbConfig, string decryptedConnectionString, string? sessionContextJson = null, bool isWidget = false)
        {
            _dbConfig = dbConfig;
            _decryptedConnectionString = decryptedConnectionString;
            _isWidget = isWidget;

            if (!string.IsNullOrEmpty(sessionContextJson))
            {
                try
                {
                    var doc = JsonDocument.Parse(sessionContextJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        _sessionContext[prop.Name] = prop.Value.ToString();
                    }
                }
                catch { }
            }
        }

        public async Task<ToolResult> ExecuteAsync(string argumentsJson, string userId)
        {
            try
            {
                var doc = JsonDocument.Parse(argumentsJson);
                var query = doc.RootElement.GetProperty("query").GetString();

                if (string.IsNullOrWhiteSpace(query)) 
                    return new ToolResult { ToolName = Name, Error = "No query provided" };

                // Validate host against SSRF
                var host = ExtractHost(_decryptedConnectionString, _dbConfig.Type);
                if (!await IsSafeHostAsync(host, _dbConfig.Type))
                {
                    return new ToolResult { ToolName = Name, Error = "Database connection uses an restricted, private, or loopback host address." };
                }

                // Security: Basic check for read-only
                var lower = query.ToLower().Trim();
                if (!lower.StartsWith("select") && !lower.StartsWith("with"))
                {
                    return new ToolResult { ToolName = Name, Error = "Only SELECT queries are permitted for security reasons." };
                }

                // Block destructive keywords (whole words only)
                string[] blocked = { "delete", "update", "insert", "drop", "truncate", "alter", "create", "grant", "revoke", "into" };
                foreach (var word in blocked)
                {
                    if (Regex.IsMatch(lower, $@"\b{word}\b"))
                    {
                        return new ToolResult { ToolName = Name, Error = $"Destructive or administrative keyword '{word}' is not permitted." };
                    }
                }

                // Extract all referenced tables
                var tables = ExtractPhysicalTables(query);

                // Check allowed tables whitelist
                var allowed = _dbConfig.AllowedTables?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

                foreach (var table in tables)
                {
                    if (!allowed.Contains(table))
                    {
                        return new ToolResult { ToolName = Name, Error = $"Access to table '{table}' is restricted. Permitted tables: {string.Join(", ", allowed)}" };
                    }
                }

                // ── Strategy A: Column-level whitelisting ─────────────────────────────
                // For tables with a restricted column list, reject SELECT * wildcards and
                // require the AI to emit explicit column names instead.
                if (!string.IsNullOrEmpty(_dbConfig.AllowedColumnsJson))
                {
                    try
                    {
                        var colMap = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                            _dbConfig.AllowedColumnsJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (colMap != null)
                        {
                            foreach (var table in tables)
                            {
                                if (!colMap.TryGetValue(table, out var colElement)) continue;
                                if (colElement.ValueKind == JsonValueKind.Null) continue; // null = all columns OK

                                var permittedCols = colElement.EnumerateArray()
                                    .Select(e => e.GetString())
                                    .Where(s => s != null)
                                    .ToList();

                                // Detect wildcard: SELECT * / SELECT t.* / SELECT alias.*
                                bool hasWildcard = Regex.IsMatch(query,
                                    @"SELECT\s+(?:[a-zA-Z0-9_]+\.)?\*",
                                    RegexOptions.IgnoreCase);

                                if (hasWildcard)
                                {
                                    return new ToolResult
                                    {
                                        ToolName = Name,
                                        Error = $"SELECT * is not permitted for table '{table}'. " +
                                                $"Use explicit column names. " +
                                                $"Permitted columns: {string.Join(", ", permittedCols!)}"
                                    };
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        return new ToolResult { ToolName = Name, Error = "Failed to parse allowed columns configuration. Query blocked for security reasons." };
                    }
                }
                // ─────────────────────────────────────────────────────────────────────

                // Parse Isolation Filters Map
                var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(_dbConfig.SessionContextFilterJson))
                {
                    try
                    {
                        var filterMap = JsonSerializer.Deserialize<Dictionary<string, string>>(_dbConfig.SessionContextFilterJson);
                        if (filterMap != null)
                        {
                            foreach (var (k, v) in filterMap) filters[k] = v;
                        }
                    }
                    catch { }
                }

                // Inject Isolation Filters if configured
                var parameters = new DynamicParameters();
                foreach (var table in tables)
                {
                    if (filters.TryGetValue(table, out var isolationCol) && !string.IsNullOrEmpty(isolationCol))
                    {
                        // Look up parameter value in session context
                        string? sessionVal = null;
                        if (_sessionContext.TryGetValue(isolationCol, out var val1)) sessionVal = val1;
                        else if (_sessionContext.TryGetValue(CamelCase(isolationCol), out var val2)) sessionVal = val2;
                        else if (_sessionContext.TryGetValue(isolationCol.Replace("_", ""), out var val3)) sessionVal = val3;

                        if (sessionVal == null)
                        {
                            var cleanCol = isolationCol.Replace("_", "").ToLowerInvariant();
                            if (cleanCol == "projectid")
                            {
                                sessionVal = _dbConfig.ProjectId.ToString();
                            }
                            else if (cleanCol == "userid" || cleanCol == "ownerid")
                            {
                                sessionVal = userId;
                            }
                        }

                        if (sessionVal == null)
                        {
                            return new ToolResult { ToolName = Name, Error = $"Missing required session context value for isolated table '{table}' (expected key matching '{isolationCol}')." };
                        }

                        // Parameterize and replace table with subquery filter
                        var paramName = $"SessionVal_{table}";
                        if (Guid.TryParse(sessionVal, out Guid guidVal))
                        {
                            parameters.Add(paramName, guidVal);
                        }
                        else
                        {
                            parameters.Add(paramName, sessionVal);
                        }

                        // Quote the isolation column name based on database type to preserve case sensitivity
                        var cleanColName = isolationCol.Trim('"', '`', '[', ']');
                        string quotedCol = _dbConfig.Type switch
                        {
                            DatabaseType.PostgreSQL => $"\"{cleanColName}\"",
                            DatabaseType.MySQL => $"`{cleanColName}`",
                            DatabaseType.SQLServer => $"[{cleanColName}]",
                            DatabaseType.SQLite => $"\"{cleanColName}\"",
                            _ => cleanColName
                        };

                        // Match FROM/JOIN followed by the table name (with optional quotes and schema prefixes)
                        string tablePattern = $@"\b(FROM|JOIN)\s+([a-zA-Z0-9_""`\[\]]+\.)?(?:([""`\[]){table}([""`\]])|{table}\b)";
                        query = Regex.Replace(query, tablePattern, $"$1 (SELECT * FROM $2$3{table}$4 WHERE {quotedCol} = @{paramName})", RegexOptions.IgnoreCase);
                    }
                }

                using IDbConnection conn = _dbConfig.Type switch
                {
                    DatabaseType.PostgreSQL => new NpgsqlConnection(_decryptedConnectionString),
                    DatabaseType.MySQL => new MySqlConnection(_decryptedConnectionString),
                    DatabaseType.SQLServer => new SqlConnection(_decryptedConnectionString),
                    DatabaseType.SQLite => new Microsoft.Data.Sqlite.SqliteConnection(_decryptedConnectionString),
                    _ => throw new NotSupportedException($"Database type {_dbConfig.Type} is not supported.")
                };

                conn.Open();

                // Run inside a transaction that we ALWAYS rollback to protect against any database mutations
                using var transaction = conn.BeginTransaction();

                var timeout = _dbConfig.MaxQueryTimeoutSeconds > 0 ? _dbConfig.MaxQueryTimeoutSeconds : 5;
                var recordsLimit = _dbConfig.MaxRecordsPerQuery > 0 ? _dbConfig.MaxRecordsPerQuery : 100;

                var rawResults = await conn.QueryAsync(query, parameters, transaction, commandTimeout: timeout);
                var resultsList = rawResults.ToList();

                transaction.Rollback();

                var finalResults = resultsList.Take(recordsLimit).ToList();

                return new ToolResult
                {
                    ToolName = Name,
                    Content = new
                    {
                        data = finalResults,
                        sql = query,
                        rowCount = resultsList.Count,
                        truncated = resultsList.Count > recordsLimit
                    }
                };
            }
            catch (Exception ex)
            {
                var userError = _isWidget ? "An error occurred while executing the database query. Please verify your query syntax or search terms." : ex.Message;
                return new ToolResult { ToolName = Name, Error = userError };
            }
        }

        private static string? ExtractHost(string connStr, DatabaseType type)
        {
            if (type == DatabaseType.SQLite) return "localhost";

            var parts = connStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                {
                    var key = kv[0].Trim().ToLower();
                    var val = kv[1].Trim();
                    if (type == DatabaseType.PostgreSQL && (key == "host" || key == "server"))
                        return val;
                    if (type == DatabaseType.MySQL && (key == "server" || key == "host"))
                        return val;
                    if (type == DatabaseType.SQLServer && (key == "server" || key == "data source" || key == "addr"))
                        return val;
                }
            }
            return null;
        }

        private static async Task<bool> IsSafeHostAsync(string? host, DatabaseType dbType)
        {
            if (dbType == DatabaseType.SQLite) return true;
            if (System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development") return true;
            if (string.IsNullOrWhiteSpace(host)) return false;

            var cleanHost = host.Split(':', ',')[0].Trim();
            if (string.IsNullOrEmpty(cleanHost)) return false;

            if (cleanHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) || cleanHost.Equals("127.0.0.1") || cleanHost.Equals("::1"))
                return false;

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(cleanHost);
                foreach (var ip in addresses)
                {
                    if (IPAddress.IsLoopback(ip)) return false;

                    var bytes = ip.GetAddressBytes();
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (bytes[0] == 10) return false;
                        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                        if (bytes[0] == 192 && bytes[1] == 168) return false;
                        if (bytes[0] == 169 && bytes[1] == 254) return false;
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if ((bytes[0] & 0xfe) == 0xfc) return false;
                        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static HashSet<string> ExtractPhysicalTables(string query)
        {
            var cleaned = Regex.Replace(query, @"/\*.*?\*/", "", RegexOptions.Singleline);
            cleaned = Regex.Replace(cleaned, @"--.*$", "", RegexOptions.Multiline);

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ctes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var withMatch = Regex.Match(cleaned, @"\bWITH\s+([a-zA-Z0-9_""`\[\]]+)\s+AS\s*\(", RegexOptions.IgnoreCase);
            while (withMatch.Success)
            {
                var cteName = withMatch.Groups[1].Value.Trim('"', '`', '[', ']');
                ctes.Add(cteName);

                var restOfWith = cleaned.Substring(withMatch.Index + withMatch.Length);
                var nextCteMatch = Regex.Match(restOfWith, @"^\s*.*?\),\s*([a-zA-Z0-9_""`\[\]]+)\s+AS\s*\(", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (nextCteMatch.Success)
                {
                    ctes.Add(nextCteMatch.Groups[1].Value.Trim('"', '`', '[', ']'));
                }
                withMatch = withMatch.NextMatch();
            }

            var rawTokens = Regex.Split(cleaned, @"([\s,()]+)");
            var tokens = rawTokens.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i].ToLower();
                if (token == "from" || token == "join")
                {
                    int j = i + 1;
                    while (j < tokens.Count)
                    {
                        var next = tokens[j];
                        var nextLower = next.ToLower();

                        if (nextLower == "where" || nextLower == "group" || nextLower == "order" ||
                            nextLower == "having" || nextLower == "limit" || nextLower == "join" ||
                            nextLower == "left" || nextLower == "right" || nextLower == "inner" ||
                            nextLower == "outer" || nextLower == "cross" || nextLower == "on" ||
                            nextLower == "using" || nextLower == "union" || nextLower == "select")
                        {
                            break;
                        }

                        if (next == "," || next == "(" || next == ")")
                        {
                            j++;
                            continue;
                        }

                        if (nextLower == "select")
                        {
                            break;
                        }

                        var tableName = next.Trim('"', '`', '[', ']');
                        var dotIndex = tableName.IndexOf('.');
                        if (dotIndex >= 0)
                        {
                            tableName = tableName.Substring(dotIndex + 1);
                        }
                        tableName = tableName.Trim('"', '`', '[', ']');

                        if (!string.IsNullOrEmpty(tableName) && !ctes.Contains(tableName))
                        {
                            tables.Add(tableName);
                        }

                        j++;
                        if (j < tokens.Count && tokens[j].ToLower() == "as")
                        {
                            j += 2;
                        }
                        else
                        {
                            if (j < tokens.Count && !new[] { ",", "join", "left", "right", "inner", "where", "group", "order", "limit", "on" }
                                .Contains(tokens[j].ToLower()))
                            {
                                j++;
                            }
                        }
                    }
                }
            }
            return tables;
        }

        private static string CamelCase(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            var parts = str.Split('_');
            var result = parts[0].ToLower();
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    result += char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
            }
            return result;
        }
    }
}
