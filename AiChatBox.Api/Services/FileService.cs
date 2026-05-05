using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class FileService(ChatDbContext db, IWebHostEnvironment env, ILogger<FileService> logger) : IFileService
    {
        private readonly ChatDbContext _db = db;
        private readonly string _uploadPath = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        private readonly ILogger<FileService> _logger = logger;

        public async Task<UploadedFile> SaveFileAsync(IFormFile file, string userId)
        {
            if (!Directory.Exists(_uploadPath)) Directory.CreateDirectory(_uploadPath);

            var fileId = Guid.NewGuid();
            var extension = Path.GetExtension(file.FileName).ToLower();
            var storedName = $"{fileId}{extension}";
            var fullPath = Path.Combine(_uploadPath, storedName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var extractedText = string.Empty;
            if (extension == ".txt" || extension == ".md" || extension == ".csv" || extension == ".json")
            {
                try
                {
                    extractedText = await File.ReadAllTextAsync(fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to extract text from {FileName}: {Message}", file.FileName, ex.Message);
                }
            }

            var uploadedFile = new UploadedFile
            {
                Id = fileId,
                UserId = userId,
                OriginalFileName = file.FileName,
                StoredFileName = storedName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                ExtractedText = extractedText,
                UploadedAt = DateTime.UtcNow
            };

            _db.UploadedFiles.Add(uploadedFile);
            await _db.SaveChangesAsync();

            return uploadedFile;
        }

        public async Task<UploadedFile?> GetFileAsync(Guid fileId, string userId)
        {
            return await _db.UploadedFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId);
        }
    }
}
