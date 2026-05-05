using Microsoft.AspNetCore.Mvc;
using AiChatBox.Api.Services;
using AiChatBox.Api.DTOs;

namespace AiChatBox.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AudioController(GroqAudioService sttService, GeminiTtsService ttsService) : ControllerBase
    {
        private readonly GroqAudioService _sttService = sttService;
        private readonly GeminiTtsService _ttsService = ttsService;

        [HttpPost("transcribe")]
        public async Task<IActionResult> Transcribe([FromForm] IFormFile audio, [FromForm] string language = "auto")
        {
            if (audio == null || audio.Length == 0) return BadRequest("No audio uploaded");

            using var ms = new MemoryStream();
            await audio.CopyToAsync(ms);
            var text = await _sttService.TranscribeAsync(ms.ToArray(), language);

            return Ok(new { text });
        }

        [HttpPost("tts")]
        public async Task<IActionResult> Tts([FromBody] TtsRequest request)
        {
            if (string.IsNullOrEmpty(request.Text)) return BadRequest("No text provided");

            try
            {
                var audioData = await _ttsService.TextToSpeechAsync(request.Text, request.Voice);
                return File(audioData, "audio/wav");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
