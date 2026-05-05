using System.Text;
using System.Text.Json;
using AiChatBox.Api.Interfaces;

namespace AiChatBox.Api.Services
{
    public class GeminiTtsService(HttpClient httpClient, IConfiguration config) : IAiAudioService
    {
        private readonly HttpClient _http = httpClient;
        private readonly string _apiKey = config["Gemini:ApiKey"] ?? "";

        public Task<string> TranscribeAsync(byte[] audioData, string language = "auto")
        {
            throw new NotImplementedException("Use GroqAudioService for STT.");
        }

        public async Task<byte[]> TextToSpeechAsync(string text, string voice = "en-US-Standard-A")
        {
            if (string.IsNullOrEmpty(_apiKey)) throw new Exception("Gemini API key missing");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_apiKey}";
            
            var payload = new
            {
                contents = new[] { new { parts = new[] { new { text = $"Convert this text to speech: {text}" } } } },
                generationConfig = new { response_mime_type = "audio/wav" }
            };

            var response = await _http.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode) throw new Exception($"TTS failed: {response.StatusCode}");

            // Note: In a real implementation, we'd parse the base64 audio from the Gemini response.
            // For the standalone demo, we return a placeholder or handle the response mapping.
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            // Simplified logic: extract base64 from response
            try {
                var base64 = result.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("inlineData").GetProperty("data").GetString();
                return Convert.FromBase64String(base64 ?? "");
            } catch {
                return Array.Empty<byte>();
            }
        }
    }
}
