using System.ComponentModel.DataAnnotations;

namespace AiChatBox.Api.DTOs
{
    public class PartnerAccountDto
    {
        public Guid Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public string? AllowedDomainPattern { get; set; }
        public int MaxTenants { get; set; }
        public int TenantCount { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal CurrentSpend { get; set; }
        public string? DefaultSystemPrompt { get; set; }
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public string? DefaultThemeSettingsJson { get; set; }
        public bool MasterKeyActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdatePartnerAccountDto
    {
        [Required, MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AllowedDomainPattern { get; set; }

        public string? DefaultSystemPrompt { get; set; }
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public string? DefaultThemeSettingsJson { get; set; }
    }

    public class ProvisionTenantRequest
    {
        [Required, MaxLength(200)]
        public string TenantName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TenantIdentifier { get; set; }

        // Optional overrides
        public string? SystemPrompt { get; set; }
        public string? Provider { get; set; }
        public string? ModelName { get; set; }
        public string? AllowedDomains { get; set; }
        public string? ThemeSettingsJson { get; set; }
        public string? EmbedSettingsJson { get; set; }
        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }
    }

    public class ProvisionTenantResponse
    {
        public Guid ProjectId { get; set; }
        public Guid ConfigurationId { get; set; }
        public string WidgetApiKey { get; set; } = string.Empty;
        public string TenantIdentifier { get; set; } = string.Empty;
    }

    public class TenantSummaryDto
    {
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? TenantIdentifier { get; set; }
        public string? Provider { get; set; }
        public string? ModelName { get; set; }
        public int SessionCount { get; set; }
        public bool HasApiKey { get; set; }
        public string EmbedSettingsJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? WebhookUrl { get; set; }
        public bool HasWebhookSecret { get; set; }
        public string? AllowedDomains { get; set; }
        public string? SystemPrompt { get; set; }
    }

    public class UpdateTenantRequest
    {
        [Required, MaxLength(200)]
        public string TenantName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TenantIdentifier { get; set; }

        public string? SystemPrompt { get; set; }
        public string? Provider { get; set; }
        public string? ModelName { get; set; }
        public string? AllowedDomains { get; set; }
        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }
    }

    public class UpdateTenantEmbedSettingsRequest
    {
        [Required, MaxLength(2000)]
        public string EmbedSettingsJson { get; set; } = string.Empty;
    }

    public class TenantTokenResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
