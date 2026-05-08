using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace AiChatBox.Api.Services
{
    public class FileProcessingService(ChatDbContext db, IConfiguration configuration, ILogger<FileProcessingService> logger)
    {
        private readonly ChatDbContext _db = db;
        private readonly ILogger<FileProcessingService> _logger = logger;
        private readonly string _uploadBasePath = configuration["FileStorage:BasePath"] ?? Path.Combine("wwwroot", "uploads", "chat");
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp",
            "application/pdf",
            "text/csv",
            "text/plain",
            "application/json",
            "text/markdown"
        };

        public async Task<UploadedFile> UploadAndProcessAsync(string userId, Stream fileStream, string fileName, string contentType)
        {
            if (!SupportedContentTypes.Contains(contentType))
            {
                throw new InvalidOperationException($"Unsupported file type: {contentType}");
            }

            var userDir = Path.Combine(_uploadBasePath, userId);
            Directory.CreateDirectory(userDir);

            var extension = Path.GetExtension(fileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(userDir, storedFileName);

            long fileSize;
            using (var output = new FileStream(filePath, FileMode.Create))
            {
                await fileStream.CopyToAsync(output);
                fileSize = output.Length;
            }

            if (fileSize > MaxFileSizeBytes)
            {
                File.Delete(filePath);
                throw new InvalidOperationException("File exceeds 10MB limit.");
            }

            string? extractedText = null;
            if (contentType.StartsWith("text/") || contentType == "application/json")
            {
                extractedText = await File.ReadAllTextAsync(filePath);
                if (extractedText.Length > 10000) extractedText = extractedText[..10000] + "... [truncated]";
            }
            else if (contentType == "application/pdf")
            {
                extractedText = ExtractPdfText(filePath, fileName, fileSize);
            }

            var fileModel = new UploadedFile
            {
                UserId = userId,
                OriginalFileName = fileName,
                StoredFileName = storedFileName,
                ContentType = contentType,
                FileSizeBytes = fileSize,
                ExtractedText = extractedText,
                UploadedAt = DateTime.UtcNow
            };

            _db.UploadedFiles.Add(fileModel);
            await _db.SaveChangesAsync();

            return fileModel;
        }

        public async Task<UploadedFile?> GetFileAsync(Guid fileId) => await _db.UploadedFiles.FindAsync(fileId);

        public async Task<bool> DeleteFileAsync(Guid fileId, string userId)
        {
            var file = await _db.UploadedFiles.FirstOrDefaultAsync(f => f.Id == fileId && f.UserId == userId);
            if (file == null) return false;

            var filePath = Path.Combine(_uploadBasePath, userId, file.StoredFileName);
            if (File.Exists(filePath)) File.Delete(filePath);

            _db.UploadedFiles.Remove(file);
            await _db.SaveChangesAsync();
            return true;
        }

        private static string ExtractPdfText(string filePath, string fileName, long fileSize)
        {
            try
            {
                using var pdf = PdfDocument.Open(filePath);
                var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
                if (text.Length > 10000) text = text[..10000] + "... [truncated]";
                return text;
            }
            catch (Exception ex)
            {
                return $"[PDF: {fileName} ({fileSize} bytes) - text extraction failed: {ex.Message}]";
            }
        }
    }
}
