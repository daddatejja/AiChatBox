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

        /// <summary>Handoff status: "ai", "queued", "active", "resolved"</summary>
        [MaxLength(20)]
        public string HandoffStatus { get; set; } = "ai";

        /// <summary>The dashboard agent user ID currently handling this session.</summary>
        [MaxLength(450)]
        public string? AgentId { get; set; }

        /// <summary>When the session entered the handoff queue.</summary>
        public DateTime? QueuedAt { get; set; }

        /// <summary>When an agent claimed the session.</summary>
        public DateTime? ClaimedAt { get; set; }
        
        // --- Flow State ---
        public Guid? ActiveFlowId { get; set; }
        public virtual ConversationFlow? ActiveFlow { get; set; }
        
        [MaxLength(100)]
        public string? CurrentNodeId { get; set; }

        public string? FlowVariablesJson { get; set; }

        [MaxLength(100)]
        public string? ExternalSenderId { get; set; }

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

        /// <summary>User feedback: 1 = thumbs up, -1 = thumbs down, null = no feedback.</summary>
        public int? Feedback { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SessionId))]
        public virtual ChatSession? Session { get; set; }

        [ForeignKey(nameof(AttachedFileId))]
        public virtual UploadedFile? AttachedFile { get; set; }
    }
}
