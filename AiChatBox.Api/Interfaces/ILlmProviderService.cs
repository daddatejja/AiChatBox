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
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ArgumentsJson { get; set; } = "{}";
        public string? ThoughtSignature { get; set; }
    }

    public interface ILlmProviderService
    {
        IAsyncEnumerable<LlmResponseChunk> StreamGenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            IEnumerable<ITool>? tools = null,
            string? modelName = null,
            string? apiKeyOverride = null,
            CancellationToken cancellationToken = default);

        Task<string> GenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            object[]? toolDeclarations = null,
            string? modelName = null,
            string? apiKeyOverride = null,
            CancellationToken cancellationToken = default);
            
        int EstimateTokenCount(string text);
    }
}
