using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiChatBox.Api.Models
{
    [Table("UploadedFiles")]
    public class UploadedFile
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        public string? ExtractedText { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
