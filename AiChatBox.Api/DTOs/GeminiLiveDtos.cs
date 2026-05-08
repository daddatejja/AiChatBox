using System.Text.Json.Serialization;

namespace AiChatBox.Api.DTOs
{
    public class GeminiLiveSetupRequest
    {
        [JsonPropertyName("setup")]
        public GeminiLiveSetup Setup { get; set; }
    }

    public class GeminiLiveSetup
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "models/gemini-2.5-flash-native-audio-latest";

        [JsonPropertyName("generationConfig")]
        public GeminiLiveGenerationConfig GenerationConfig { get; set; } = new();

        [JsonPropertyName("systemInstruction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiLiveSystemInstruction? SystemInstruction { get; set; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object[]? Tools { get; set; }

        [JsonPropertyName("inputAudioTranscription")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? InputAudioTranscription { get; set; }

        [JsonPropertyName("outputAudioTranscription")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? OutputAudioTranscription { get; set; }
    }

    public class GeminiLiveGenerationConfig
    {
        [JsonPropertyName("responseModalities")]
        public string[] ResponseModalities { get; set; } = ["audio"];

        [JsonPropertyName("speechConfig")]
        public GeminiLiveSpeechConfig SpeechConfig { get; set; } = new();
    }

    public class GeminiLiveSpeechConfig
    {
        [JsonPropertyName("voiceConfig")]
        public GeminiLiveVoiceConfig VoiceConfig { get; set; } = new();
    }

    public class GeminiLiveVoiceConfig
    {
        [JsonPropertyName("prebuiltVoiceConfig")]
        public GeminiLivePrebuiltVoiceConfig PrebuiltVoiceConfig { get; set; } = new();
    }

    public class GeminiLivePrebuiltVoiceConfig
    {
        [JsonPropertyName("voiceName")]
        public string VoiceName { get; set; } = "Aoede";
    }

    public class GeminiLiveSystemInstruction
    {
        [JsonPropertyName("parts")]
        public GeminiLivePart[] Parts { get; set; }
    }

    public class GeminiLivePart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("inlineData")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiLiveInlineData? InlineData { get; set; }

        [JsonPropertyName("thought")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Thought { get; set; }
    }

    public class GeminiLiveInlineData
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; }

        [JsonPropertyName("data")]
        public string Data { get; set; }
    }

    public class GeminiLiveRealtimeInputRequest
    {
        [JsonPropertyName("realtimeInput")]
        public GeminiLiveRealtimeInput RealtimeInput { get; set; }
    }

    public class GeminiLiveRealtimeInput
    {
        [JsonPropertyName("mediaChunks")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiLiveMediaChunk[]? MediaChunks { get; set; }

        // myDairyApp uses 'audio' property, which might be an alternative schema
        [JsonPropertyName("audio")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiLiveMediaChunk? Audio { get; set; }

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("turnComplete")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? TurnComplete { get; set; }
    }

    public class GeminiLiveMediaChunk
    {
        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = "audio/pcm;rate=16000";

        [JsonPropertyName("data")]
        public string Data { get; set; }
    }

    public class GeminiLiveClientContentRequest
    {
        [JsonPropertyName("clientContent")]
        public GeminiLiveClientContent ClientContent { get; set; }
    }

    public class GeminiLiveClientContent
    {
        [JsonPropertyName("turns")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiLiveTurn[]? Turns { get; set; }

        [JsonPropertyName("turnComplete")]
        public bool TurnComplete { get; set; }
    }

    public class GeminiLiveTurn
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("parts")]
        public GeminiLivePart[] Parts { get; set; }
    }

    public class GeminiLiveServerResponse
    {
        [JsonPropertyName("serverContent")]
        public GeminiLiveServerContent? ServerContent { get; set; }

        [JsonPropertyName("toolCall")]
        public GeminiLiveToolCall? ToolCall { get; set; }

        [JsonPropertyName("error")]
        public GeminiLiveError? Error { get; set; }
    }

    public class GeminiLiveError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }

    public class GeminiLiveServerContent
    {
        [JsonPropertyName("modelTurn")]
        public GeminiLiveModelTurn? ModelTurn { get; set; }

        [JsonPropertyName("outputTranscription")]
        public GeminiLiveTranscription? OutputTranscription { get; set; }

        [JsonPropertyName("inputTranscription")]
        public GeminiLiveTranscription? InputTranscription { get; set; }

        [JsonPropertyName("interrupted")]
        public object? Interrupted { get; set; }

        [JsonPropertyName("turnComplete")]
        public object? TurnComplete { get; set; }
    }

    public class GeminiLiveTranscription
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class GeminiLiveModelTurn
    {
        [JsonPropertyName("parts")]
        public GeminiLivePart[] Parts { get; set; }
    }

    public class GeminiLiveToolCall
    {
        [JsonPropertyName("functionCalls")]
        public GeminiLiveFunctionCall[] FunctionCalls { get; set; }
    }

    public class GeminiLiveFunctionCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("args")]
        public Dictionary<string, object> Args { get; set; }
    }
}
