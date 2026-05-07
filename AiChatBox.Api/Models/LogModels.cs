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

        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(200)]
        public string? Endpoint { get; set; }

        public int InputTokens { get; set; }

        public int OutputTokens { get; set; }

        public int DurationMs { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
