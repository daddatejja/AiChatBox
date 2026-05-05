using AiChatBox.Api.Interfaces;

namespace AiChatBox.Api.Services
{
    public class LlmProviderFactory(GeminiServerService geminiService, GrokServerService grokService)
    {
        private readonly GeminiServerService _geminiService = geminiService;
        private readonly GrokServerService _grokService = grokService;

        public ILlmProviderService GetProvider(string providerName)
        {
            return providerName?.ToLowerInvariant() switch
            {
                "grok" => _grokService,
                "gemini" => _geminiService,
                _ => _geminiService
            };
        }
    }
}
