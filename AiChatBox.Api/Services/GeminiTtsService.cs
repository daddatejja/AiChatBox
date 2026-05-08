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
            throw new NotSupportedException("GeminiTtsService only supports Text-to-Speech. Use GroqAudioService for transcription.");
        }

        public async Task<byte[]> TextToSpeechAsync(string text, string voice = "en-US-Standard-A")
        {
            if (string.IsNullOrEmpty(_apiKey)) throw new Exception("Gemini API key missing");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-tts-preview:generateContent?key={_apiKey}";

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = "You are a text-to-speech engine. Convert the user's text to natural speech. Output only audio data with no additional text." } }
                },
                contents = new[] { new { parts = new[] { new { text } } } },
                generationConfig = new { response_mime_type = "audio/wav" }
            };

            var response = await _http.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"TTS failed ({response.StatusCode}): {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            try
            {
                var base64 = result.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("inlineData").GetProperty("data").GetString();
                return Convert.FromBase64String(base64 ?? "");
            }
            catch
            {
                return Array.Empty<byte>();
            }
        }
    }
}

