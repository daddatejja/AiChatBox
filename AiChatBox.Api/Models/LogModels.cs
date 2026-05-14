using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AiChatBox.Api.Models
{
    [Table("AiRequestLogs")]
    public class AiRequestLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? SessionId { get; set; }
        public Guid? ProjectId { get; set; }

        [ForeignKey(nameof(SessionId))]
        public virtual ChatSession? Session { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        public Guid? ConfigurationId { get; set; }

        [ForeignKey(nameof(ConfigurationId))]
        public virtual ProjectConfiguration? Configuration { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(200)]
        public string? Endpoint { get; set; }
        
        [MaxLength(50)]
        public string? Provider { get; set; }
        
        [MaxLength(100)]
        public string? Model { get; set; }

        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int DurationMs { get; set; }

        public string? RawRequest { get; set; }
        public string? RawResponse { get; set; }
        public string? ErrorMessage { get; set; }

        public bool IsPinned { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TimelineEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Type { get; set; } = string.Empty; 
        public string? Content { get; set; }
        public string? Transcription { get; set; }
        public string? Meta { get; set; }
        public int DurationMs { get; set; }
    }
}
