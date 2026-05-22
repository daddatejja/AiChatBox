using System.ComponentModel.DataAnnotations;

namespace AiChatBox.Api.DTOs
{
    public class PartnerListItemDto
    {
        public Guid Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public int TenantCount { get; set; }
        public int MaxTenants { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal CurrentSpend { get; set; }
        public bool MasterKeyActive { get; set; }
        public string? AllowedDomainPattern { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreatePartnerDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AllowedDomainPattern { get; set; }

        public int MaxTenants { get; set; } = 100;
        public decimal CreditLimit { get; set; } = 0;
    }

    public class UpdatePartnerDto
    {
        [Required, MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AllowedDomainPattern { get; set; }

        public int MaxTenants { get; set; } = 100;
        public decimal CreditLimit { get; set; } = 0;
    }

    public class UserListItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ProjectCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChangeUserRoleDto
    {
        [Required]
        public string Role { get; set; } = string.Empty; // StandardUser, PartnerDeveloper, SystemAdmin
    }

    public class AdminProjectListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public Guid? PartnerAccountId { get; set; }
        public string? PartnerCompanyName { get; set; }
        public string? TenantIdentifier { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int SessionCount { get; set; }
        public int MessageCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PlatformAnalyticsDto
    {
        public int TotalUsers { get; set; }
        public int TotalPartners { get; set; }
        public int TotalProjects { get; set; }
        public int TotalSessions { get; set; }
        public int TotalRequests { get; set; }
        public long TotalTokens { get; set; }
        public double ErrorRate { get; set; }
        public double AvgResponseMs { get; set; }
    }

    public class PlatformVolumePointDto
    {
        public DateTime Date { get; set; }
        public int Requests { get; set; }
        public int Sessions { get; set; }
    }

    public class ProviderStatsDto
    {
        public string Provider { get; set; } = string.Empty;
        public int Requests { get; set; }
    }
}
