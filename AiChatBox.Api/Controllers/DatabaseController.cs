using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DatabaseController(ChatDbContext db, EncryptionService encryption) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly EncryptionService _encryption = encryption;

        [HttpGet("{projectId}")]
        public async Task<IActionResult> GetDatabase(Guid projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects
                .Include(p => p.Database)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);

            if (project == null) return NotFound();

            if (project.Database == null) return Ok(null);

            return Ok(new
            {
                project.Database.Id,
                project.Database.Type,
                project.Database.SchemaDefinition,
                project.Database.AllowedTables,
                project.Database.AllowedColumnsJson,
                project.Database.MaxQueryTimeoutSeconds,
                project.Database.MaxRecordsPerQuery,
                project.Database.SessionContextFilterJson,
                HasConnectionString = !string.IsNullOrEmpty(project.Database.ConnectionString)
            });
        }

        [HttpPost("{projectId}")]
        public async Task<IActionResult> UpdateDatabase(Guid projectId, [FromBody] DatabaseUpdateRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects
                .Include(p => p.Database)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);

            if (project == null) return NotFound();

            if (project.Database == null)
            {
                project.Database = new ProjectDatabase { ProjectId = projectId };
                _db.ProjectDatabases.Add(project.Database);
            }

            project.Database.Type = request.Type;
            project.Database.SchemaDefinition = request.SchemaDefinition;
            project.Database.AllowedTables = request.AllowedTables;
            project.Database.AllowedColumnsJson = request.AllowedColumnsJson;
            project.Database.MaxQueryTimeoutSeconds = request.MaxQueryTimeoutSeconds > 0 && request.MaxQueryTimeoutSeconds <= 30
                ? request.MaxQueryTimeoutSeconds
                : 5;
            project.Database.MaxRecordsPerQuery = request.MaxRecordsPerQuery > 0 && request.MaxRecordsPerQuery <= 1000
                ? request.MaxRecordsPerQuery
                : 100;
            project.Database.SessionContextFilterJson = request.SessionContextFilterJson;

            if (!string.IsNullOrEmpty(request.ConnectionString))
            {
                project.Database.ConnectionString = _encryption.Encrypt(request.ConnectionString);
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                project.Database.Id,
                project.Database.Type,
                project.Database.SchemaDefinition,
                project.Database.AllowedTables,
                project.Database.AllowedColumnsJson,
                project.Database.MaxQueryTimeoutSeconds,
                project.Database.MaxRecordsPerQuery,
                project.Database.SessionContextFilterJson,
                HasConnectionString = !string.IsNullOrEmpty(project.Database.ConnectionString)
            });
        }

        [HttpPost("{projectId}/detect-schema")]
        public async Task<IActionResult> DetectSchema(Guid projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var db = await _db.ProjectDatabases
                .Include(d => d.Project)
                .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.Project.UserId == userId);

            if (db == null || string.IsNullOrEmpty(db.ConnectionString))
                return BadRequest("Database not configured or connection string missing.");

            var connectionString = _encryption.Decrypt(db.ConnectionString);
            var schema = new StringBuilder();

            try
            {
                if (db.Type == DatabaseType.PostgreSQL)
                {
                    using var conn = new Npgsql.NpgsqlConnection(connectionString);
                    await conn.OpenAsync();
                    using var cmd = new Npgsql.NpgsqlCommand(@"
                        SELECT table_name, column_name, data_type 
                        FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        ORDER BY table_name, ordinal_position", conn);
                    using var reader = await cmd.ExecuteReaderAsync();
                    string currentTable = "";
                    while (await reader.ReadAsync())
                    {
                        var table = reader.GetString(0);
                        if (table != currentTable)
                        {
                            if (currentTable != "") schema.AppendLine(");");
                            schema.AppendLine($"CREATE TABLE {table} (");
                            currentTable = table;
                        }
                        else schema.AppendLine(",");
                        schema.Append($"  {reader.GetString(1)} {reader.GetString(2)}");
                    }
                    if (currentTable != "") schema.AppendLine("\n);");
                }
                else if (db.Type == DatabaseType.MySQL)
                {
                    using var conn = new MySqlConnector.MySqlConnection(connectionString);
                    await conn.OpenAsync();
                    using var cmd = new MySqlConnector.MySqlCommand("SHOW TABLES", conn);
                    var tables = new List<string>();
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
                    }

                    foreach (var table in tables)
                    {
                        using var cmdShow = new MySqlConnector.MySqlCommand($"SHOW CREATE TABLE {table}", conn);
                        using var readerShow = await cmdShow.ExecuteReaderAsync();
                        if (await readerShow.ReadAsync())
                        {
                            schema.AppendLine(readerShow.GetString(1) + ";\n");
                        }
                    }
                }
                else if (db.Type == DatabaseType.SQLite)
                {
                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                    await conn.OpenAsync();
                    using var cmd = new Microsoft.Data.Sqlite.SqliteCommand("SELECT sql FROM sqlite_master WHERE type='table'", conn);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        schema.AppendLine(reader.GetString(0) + ";\n");
                    }
                }
                else if (db.Type == DatabaseType.SQLServer)
                {
                    using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                    await conn.OpenAsync();
                    using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT t.name AS table_name, c.name AS column_name, ty.name AS data_type
                        FROM sys.tables t
                        JOIN sys.columns c ON t.object_id = c.object_id
                        JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                        ORDER BY t.name, c.column_id", conn);
                    using var reader = await cmd.ExecuteReaderAsync();
                    string currentTable = "";
                    while (await reader.ReadAsync())
                    {
                        var table = reader.GetString(0);
                        if (table != currentTable)
                        {
                            if (currentTable != "") schema.AppendLine(");");
                            schema.AppendLine($"CREATE TABLE {table} (");
                            currentTable = table;
                        }
                        else schema.AppendLine(",");
                        schema.Append($"  {reader.GetString(1)} {reader.GetString(2)}");
                    }
                    if (currentTable != "") schema.AppendLine("\n);");
                }

                return Ok(new { schema = schema.ToString() });
            }
            catch (Exception ex)
            {
                var fullError = ex.Message;
                if (ex.InnerException != null) fullError += " -> " + ex.InnerException.Message;
                return BadRequest($"Failed to detect schema: {fullError}");
            }
        }

        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteDatabase(Guid projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects
                .Include(p => p.Database)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);

            if (project == null) return NotFound();
            if (project.Database == null) return NoContent();

            _db.ProjectDatabases.Remove(project.Database);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }

    public class DatabaseUpdateRequest
    {
        public DatabaseType Type { get; set; }
        public string? ConnectionString { get; set; }
        public string? SchemaDefinition { get; set; }
        public string? AllowedTables { get; set; }
        /// <summary>JSON map: { "TableName": ["col1", "col2"] | null }</summary>
        public string? AllowedColumnsJson { get; set; }
        public int MaxQueryTimeoutSeconds { get; set; }
        public int MaxRecordsPerQuery { get; set; }
        public string? SessionContextFilterJson { get; set; }
    }
}
