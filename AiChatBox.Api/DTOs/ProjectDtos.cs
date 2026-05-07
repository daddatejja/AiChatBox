namespace AiChatBox.Api.DTOs
{
    public class ProjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ApiKeyCount { get; set; }
    }

    public class CreateProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";
        public string Provider { get; set; } = "gemini";
        public string ModelName { get; set; } = "gemini-1.5-flash";
    }

    public class UpdateProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }
    }

    public class CustomToolDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ParametersJsonSchema { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
