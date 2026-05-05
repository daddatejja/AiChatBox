using System.Text.Json.Nodes;

namespace AiChatBox.Api.Interfaces
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        JsonObject ParametersSchema { get; }
        Task<ToolResult> ExecuteAsync(string argumentsJson, string userId);
    }

    public class ToolResult
    {
        public string ToolName { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? Error { get; set; }
        public bool Success => Error == null;
    }
}
