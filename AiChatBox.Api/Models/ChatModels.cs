using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiChatBox.Api.Models
{
    [Table("ChatSessions")]
    public class ChatSession
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public Guid? ProjectId { get; set; }
        
        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        public Guid? ConfigurationId { get; set; }

        [ForeignKey(nameof(ConfigurationId))]
        public virtual ProjectConfiguration? Configuration { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public bool IsArchived { get; set; }

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    [Table("ChatMessages")]
    public class ChatMessage
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SessionId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "user";

        [Required]
        public string Content { get; set; } = string.Empty;

        public string? ImageDataUrl { get; set; }

        public Guid? AttachedFileId { get; set; }

        public int TokenCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SessionId))]
        public virtual ChatSession? Session { get; set; }

        [ForeignKey(nameof(AttachedFileId))]
        public virtual UploadedFile? AttachedFile { get; set; }
    }
}
