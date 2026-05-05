using AiChatBox.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController(IFileService fileService) : ControllerBase
    {
        private readonly IFileService _fileService = fileService;

        private string UserId => Request.Headers["X-User-Id"].ToString() ?? "standalone-user";

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded");

            try
            {
                var uploadedFile = await _fileService.SaveFileAsync(file, UserId);
                return Ok(new
                {
                    Id = uploadedFile.Id,
                    Name = uploadedFile.OriginalFileName,
                    Size = uploadedFile.FileSizeBytes,
                    Type = uploadedFile.ContentType
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetFileInfo(Guid id)
        {
            var file = await _fileService.GetFileAsync(id, UserId);
            if (file == null) return NotFound();

            return Ok(new
            {
                Id = file.Id,
                Name = file.OriginalFileName,
                Size = file.FileSizeBytes,
                Type = file.ContentType,
                UploadedAt = file.UploadedAt
            });
        }
    }
}
