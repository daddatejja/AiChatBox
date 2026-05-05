using AiChatBox.Api.DTOs;

namespace AiChatBox.Api.Interfaces
{
    public class LlmResponseChunk
    {
        public string? Text { get; set; }
        public ToolCall? ToolCall { get; set; }
    }

    public class ToolCall
    {
        public string Name { get; set; } = string.Empty;
        public string ArgumentsJson { get; set; } = "{}";
    }

    public interface ILlmProviderService
    {
        IAsyncEnumerable<LlmResponseChunk> StreamGenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            IEnumerable<ITool>? tools = null,
            string? modelName = null,
            CancellationToken cancellationToken = default);

        Task<string> GenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            object[]? toolDeclarations = null,
            string? modelName = null,
            CancellationToken cancellationToken = default);
            
        int EstimateTokenCount(string text);
    }
}
