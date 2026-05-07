using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace AiChatBox.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Project> Projects { get; set; } = [];
    }

    public class Project
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";
        
        public string Provider { get; set; } = "gemini";
        public string ModelName { get; set; } = "gemini-1.5-flash";

        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ApiKey> ApiKeys { get; set; } = [];
        public ICollection<CustomTool> CustomTools { get; set; } = [];
        public ICollection<ChatSession> Sessions { get; set; } = [];
    }

    public class ApiKey
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Label { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }

    public class CustomTool
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public string ParametersJsonSchema { get; set; } = "{}";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
