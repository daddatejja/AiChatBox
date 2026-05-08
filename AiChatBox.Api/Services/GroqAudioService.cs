using System.Net.Http.Headers;
using AiChatBox.Api.Interfaces;

namespace AiChatBox.Api.Services
{
    public class GroqAudioService(HttpClient httpClient, IConfiguration config) : IAiAudioService
    {
        private readonly HttpClient _http = httpClient;
        private readonly string _apiKey = config["Grok:ApiKey"] ?? "";

        public async Task<string> TranscribeAsync(byte[] audioData, string language = "auto")
        {
            if (string.IsNullOrEmpty(_apiKey)) return "Error: Groq API key missing";

            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            content.Add(audioContent, "file", "speech.wav");
            content.Add(new StringContent("whisper-large-v3"), "model");
            
            if (language != "auto")
                content.Add(new StringContent(language), "language");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/audio/transcriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return $"Error: STT failed ({response.StatusCode})";

            var result = await response.Content.ReadFromJsonAsync<GroqSttResponse>();
            return result?.Text ?? "";
        }

        public Task<byte[]> TextToSpeechAsync(string text, string voice = "en-US-Standard-A")
        {
            throw new NotSupportedException("GroqAudioService only supports Speech-to-Text. Use GeminiTtsService for TTS.");
        }

        private class GroqSttResponse { public string Text { get; set; } = ""; }
    }
}
