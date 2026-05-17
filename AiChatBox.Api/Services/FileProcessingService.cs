using System.Text;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using UglyToad.PdfPig;

namespace AiChatBox.Api.Services
{
    public class FileProcessingService(ChatDbContext db, IConfiguration configuration, EmbeddingService embeddingService, ILogger<FileProcessingService> logger)
    {
        private readonly ChatDbContext _db = db;
        private readonly EmbeddingService _embeddingService = embeddingService;
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

        public async Task<KnowledgeDocument> ProcessKnowledgeDocumentAsync(
            Guid projectId, 
            Stream fileStream, 
            string fileName, 
            string contentType, 
            string? geminiApiKey = null,
            int chunkSize = 1000,
            int chunkOverlap = 200,
            string chunkingStrategy = "character")
        {
            if (!SupportedContentTypes.Contains(contentType))
                throw new InvalidOperationException($"Unsupported file type: {contentType}");

            var projectDir = Path.Combine(_uploadBasePath, "knowledge", projectId.ToString());
            Directory.CreateDirectory(projectDir);

            var extension = Path.GetExtension(fileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(projectDir, storedFileName);

            long fileSize;
            using (var output = new FileStream(filePath, FileMode.Create))
            {
                await fileStream.CopyToAsync(output);
                fileSize = output.Length;
            }

            string extractedText = "";
            if (contentType.StartsWith("text/") || contentType == "application/json")
            {
                extractedText = await File.ReadAllTextAsync(filePath);
            }
            else if (contentType == "application/pdf")
            {
                extractedText = ExtractFullPdfText(filePath);
            }

            var docModel = await _db.KnowledgeDocuments
                .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.FileName == fileName && d.FileSize == fileSize);

            if (docModel == null)
            {
                docModel = new KnowledgeDocument
                {
                    ProjectId = projectId,
                    FileName = fileName,
                    ContentType = contentType,
                    FileSize = fileSize,
                    StoredFileName = storedFileName,
                    Status = KnowledgeDocumentStatus.Processing,
                    ChunkSize = chunkSize,
                    ChunkOverlap = chunkOverlap,
                    ChunkingStrategy = chunkingStrategy
                };
                _db.KnowledgeDocuments.Add(docModel);
            }
            else
            {
                // Clean up old chunks if retrying
                var oldChunks = _db.DocumentChunks.Where(c => c.DocumentId == docModel.Id);
                _db.DocumentChunks.RemoveRange(oldChunks);
                docModel.Status = KnowledgeDocumentStatus.Processing;
                docModel.ErrorMessage = null;
                docModel.StoredFileName = storedFileName; 
                docModel.ChunkSize = chunkSize;
                docModel.ChunkOverlap = chunkOverlap;
                docModel.ChunkingStrategy = chunkingStrategy;
            }

            await _db.SaveChangesAsync();

            try
            {
                var allChunks = ChunkText(extractedText, docModel.ChunkSize, docModel.ChunkOverlap, docModel.ChunkingStrategy);
                int totalSuccess = 0;
                int batchSize = 100;

                for (int i = 0; i < allChunks.Count; i += batchSize)
                {
                    var batch = allChunks.Skip(i).Take(batchSize).ToList();
                    try
                    {
                        var embeddings = await _embeddingService.GetBatchEmbeddingsAsync(batch, geminiApiKey, projectId: projectId);
                        for (int j = 0; j < embeddings.Count; j++)
                        {
                            var chunk = new DocumentChunk
                            {
                                DocumentId = docModel.Id,
                                Content = batch[j],
                                Embedding = embeddings[j],
                                ChunkIndex = i + j
                            };
                            _db.DocumentChunks.Add(chunk);
                        }
                        totalSuccess += batch.Count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to embed batch starting at {Index} for document {DocId}", i, docModel.Id);
                        docModel.ErrorMessage = ex.Message;
                        break; 
                    }
                }

                if (totalSuccess == 0 && allChunks.Count > 0)
                {
                    docModel.Status = KnowledgeDocumentStatus.Failed;
                    docModel.ErrorMessage = "Failed to generate embeddings. " + (docModel.ErrorMessage ?? "Check your API key.");
                }
                else if (totalSuccess < allChunks.Count)
                {
                    docModel.Status = KnowledgeDocumentStatus.Failed;
                }
                else
                {
                    docModel.Status = KnowledgeDocumentStatus.Completed;
                    docModel.IsProcessed = true;
                }
            }
            catch (Exception ex)
            {
                docModel.Status = KnowledgeDocumentStatus.Failed;
                docModel.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Unexpected error processing document {DocId}", docModel.Id);
            }

            await _db.SaveChangesAsync();
            return docModel;
        }

        private static List<string> ChunkText(string text, int chunkSize, int overlap, string strategy)
        {
            return strategy.ToLowerInvariant() switch
            {
                "line" => ChunkTextLine(text, chunkSize > 0 ? chunkSize / 100 : 10, overlap > 0 ? overlap / 100 : 2),
                "recursive" => ChunkTextRecursive(text, chunkSize, overlap),
                _ => ChunkTextCharacter(text, chunkSize, overlap)
            };
        }

        private static List<string> ChunkTextCharacter(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            if (overlap >= chunkSize) overlap = chunkSize / 2;

            int start = 0;
            while (start < text.Length)
            {
                int length = Math.Min(chunkSize, text.Length - start);
                chunks.Add(text.Substring(start, length));

                start += (chunkSize - overlap);
                if (start >= text.Length) break;
            }
            return chunks;
        }

        private static List<string> ChunkTextLine(string text, int lineCount, int lineOverlap)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lineCount <= 0) lineCount = 10;
            if (lineOverlap >= lineCount) lineOverlap = lineCount / 2;

            int start = 0;
            while (start < lines.Length)
            {
                int length = Math.Min(lineCount, lines.Length - start);
                var batchLines = lines.Skip(start).Take(length);
                chunks.Add(string.Join("\n", batchLines));

                start += (lineCount - lineOverlap);
                if (start >= lines.Length) break;
            }
            return chunks;
        }

        private static List<string> ChunkTextRecursive(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var currentChunk = new StringBuilder();

            foreach (var para in paragraphs)
            {
                var trimmedPara = para.Trim();
                if (string.IsNullOrEmpty(trimmedPara)) continue;

                if (trimmedPara.Length > chunkSize)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }

                    var subChunks = ChunkTextCharacter(trimmedPara, chunkSize, overlap);
                    chunks.AddRange(subChunks);
                    continue;
                }

                if (currentChunk.Length + trimmedPara.Length + 2 > chunkSize)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();

                    if (overlap > 0 && chunks.Count > 0)
                    {
                        var lastChunk = chunks[^1];
                        var overlapStart = Math.Max(0, lastChunk.Length - overlap);
                        currentChunk.Append(lastChunk[overlapStart..].TrimStart());
                        if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                    }
                }

                if (currentChunk.Length > 0) currentChunk.Append("\n\n");
                currentChunk.Append(trimmedPara);
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }

        private static string ExtractFullPdfText(string filePath)
        {
            try
            {
                using var pdf = PdfDocument.Open(filePath);
                return string.Join("\n", pdf.GetPages().Select(p => p.Text));
            }
            catch (Exception ex)
            {
                return $"[PDF Extraction Failed: {ex.Message}]";
            }
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
