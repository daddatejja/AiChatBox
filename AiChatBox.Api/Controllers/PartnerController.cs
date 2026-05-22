using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AiChatBox.Api.Controllers
{
    [Authorize(Policy = "PartnerOrAdmin")]
    [ApiController]
    [Route("api/[controller]")]
    public class PartnerController(
        ChatDbContext db,
        ApiKeyService apiKeyService,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly ApiKeyService _apiKeyService = apiKeyService;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly IConfiguration _configuration = configuration;

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        private async Task<PartnerAccount?> GetCurrentPartnerAsync()
        {
            // If authenticated via X-Master-Key, context.Items["CurrentPartner"] is set
            if (HttpContext.Items.TryGetValue("CurrentPartner", out var pObj) && pObj is PartnerAccount partner)
            {
                return partner;
            }

            // Otherwise check by current logged-in user
            var partnerAcc = await _db.PartnerAccounts
                .Include(pa => pa.Owner)
                .FirstOrDefaultAsync(pa => pa.OwnerId == UserId);

            if (partnerAcc == null)
            {
                var user = await _userManager.FindByIdAsync(UserId);
                if (user != null && user.AccountType == UserRole.SystemAdmin)
                {
                    // For SystemAdmin, allow them to manage/view the first partner account,
                    // or create a default partner account if none exists.
                    partnerAcc = await _db.PartnerAccounts
                        .Include(pa => pa.Owner)
                        .FirstOrDefaultAsync();

                    if (partnerAcc == null)
                    {
                        partnerAcc = new PartnerAccount
                        {
                            CompanyName = "System Default Partner",
                            OwnerId = UserId,
                            MaxTenants = 100,
                            MasterKeyHash = string.Empty,
                            MasterKeyActive = true
                        };
                        _db.PartnerAccounts.Add(partnerAcc);
                        await _db.SaveChangesAsync();
                    }
                }
            }

            return partnerAcc;
        }

        [HttpGet("account")]
        public async Task<ActionResult<PartnerAccountDto>> GetAccount()
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var tenantCount = await _db.Projects.CountAsync(p => p.PartnerAccountId == partner.Id);

            return Ok(new PartnerAccountDto
            {
                Id = partner.Id,
                CompanyName = partner.CompanyName,
                OwnerEmail = partner.Owner?.Email ?? string.Empty,
                AllowedDomainPattern = partner.AllowedDomainPattern,
                MaxTenants = partner.MaxTenants,
                TenantCount = tenantCount,
                CreditLimit = partner.CreditLimit,
                CurrentSpend = partner.CurrentSpend,
                DefaultSystemPrompt = partner.DefaultSystemPrompt,
                DefaultProvider = partner.DefaultProvider,
                DefaultModel = partner.DefaultModel,
                DefaultThemeSettingsJson = partner.DefaultThemeSettingsJson,
                MasterKeyActive = partner.MasterKeyActive,
                CreatedAt = partner.CreatedAt
            });
        }

        [HttpPut("account")]
        public async Task<IActionResult> UpdateAccount(UpdatePartnerAccountDto model)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            partner.CompanyName = model.CompanyName;
            partner.AllowedDomainPattern = model.AllowedDomainPattern;
            partner.DefaultSystemPrompt = model.DefaultSystemPrompt;
            partner.DefaultProvider = model.DefaultProvider;
            partner.DefaultModel = model.DefaultModel;
            partner.DefaultThemeSettingsJson = model.DefaultThemeSettingsJson;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("master-key/rotate")]
        public async Task<ActionResult> RotateMasterKey()
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var rawKey = await _apiKeyService.GenerateMasterKeyAsync(partner.Id);
            return Ok(new { masterKey = rawKey });
        }

        [HttpGet("tenants")]
        public async Task<ActionResult<IEnumerable<TenantSummaryDto>>> GetTenants()
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var tenants = await _db.Projects
                .Where(p => p.PartnerAccountId == partner.Id)
                .Include(p => p.ApiKeys)
                .Include(p => p.Sessions)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new TenantSummaryDto
                {
                    ProjectId = p.Id,
                    Name = p.Name,
                    TenantIdentifier = p.TenantIdentifier,
                    Provider = p.Provider,
                    ModelName = p.ModelName,
                    SessionCount = p.Sessions.Count,
                    HasApiKey = p.ApiKeys.Any(k => k.IsActive),
                    EmbedSettingsJson = p.EmbedSettingsJson,
                    CreatedAt = p.CreatedAt,
                    WebhookUrl = p.WebhookUrl,
                    HasWebhookSecret = !string.IsNullOrEmpty(p.WebhookSecret),
                    AllowedDomains = p.AllowedDomains,
                    SystemPrompt = p.SystemPrompt
                })
                .ToListAsync();

            return Ok(tenants);
        }

        [HttpPost("tenants")]
        public async Task<ActionResult<ProvisionTenantResponse>> ProvisionTenant(ProvisionTenantRequest model)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            // 1. Validate limit
            var currentTenantCount = await _db.Projects.CountAsync(p => p.PartnerAccountId == partner.Id);
            if (currentTenantCount >= partner.MaxTenants)
            {
                return Conflict(new { message = $"Tenant provisioning limit reached ({partner.MaxTenants} max)." });
            }

            // 2. Validate tenant identifier uniqueness if provided
            if (!string.IsNullOrEmpty(model.TenantIdentifier))
            {
                var exists = await _db.Projects.AnyAsync(p => p.TenantIdentifier == model.TenantIdentifier && p.PartnerAccountId == partner.Id);
                if (exists)
                {
                    return BadRequest(new { message = $"Tenant identifier '{model.TenantIdentifier}' is already in use." });
                }
            }

            // 3. Fallbacks
            var systemPrompt = model.SystemPrompt ?? partner.DefaultSystemPrompt ?? "You are a helpful AI assistant.";
            var provider = model.Provider ?? partner.DefaultProvider ?? "gemini";
            var modelName = model.ModelName ?? partner.DefaultModel ?? "gemini-3.1-flash-lite-preview";
            var embedSettings = model.EmbedSettingsJson ?? "{\"showPrompt\":true,\"showKnowledgeBase\":true,\"showRules\":true,\"showWidgetCustomization\":true}";

            // 4. Create Project
            var project = new Project
            {
                Name = model.TenantName,
                UserId = partner.OwnerId, // Set project owner to partner's user ID
                PartnerAccountId = partner.Id,
                TenantIdentifier = model.TenantIdentifier,
                SystemPrompt = systemPrompt,
                Provider = provider,
                ModelName = modelName,
                AllowedDomains = model.AllowedDomains ?? partner.AllowedDomainPattern,
                EmbedSettingsJson = embedSettings,
                WebhookUrl = model.WebhookUrl,
                WebhookSecret = model.WebhookSecret
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            // 5. Create Default Configuration
            var configuration = new ProjectConfiguration
            {
                ProjectId = project.Id,
                Name = "Default",
                SystemPrompt = systemPrompt
            };

            _db.Configurations.Add(configuration);
            await _db.SaveChangesAsync();

            // 6. Generate Widget API Key
            var (rawKey, _) = await _apiKeyService.GenerateApiKeyAsync(project.Id, "Default Widget Key", configuration.Id);

            return Ok(new ProvisionTenantResponse
            {
                ProjectId = project.Id,
                ConfigurationId = configuration.Id,
                WidgetApiKey = rawKey,
                TenantIdentifier = project.TenantIdentifier ?? string.Empty
            });
        }

        [HttpGet("tenants/{tenantId}")]
        public async Task<ActionResult<TenantSummaryDto>> GetTenant(Guid tenantId)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var p = await _db.Projects
                .Include(p => p.ApiKeys)
                .Include(p => p.Sessions)
                .FirstOrDefaultAsync(p => p.Id == tenantId && p.PartnerAccountId == partner.Id);

            if (p == null) return NotFound(new { message = "Tenant project not found." });

            return Ok(new TenantSummaryDto
            {
                ProjectId = p.Id,
                Name = p.Name,
                TenantIdentifier = p.TenantIdentifier,
                Provider = p.Provider,
                ModelName = p.ModelName,
                SessionCount = p.Sessions.Count,
                HasApiKey = p.ApiKeys.Any(k => k.IsActive),
                EmbedSettingsJson = p.EmbedSettingsJson,
                CreatedAt = p.CreatedAt,
                WebhookUrl = p.WebhookUrl,
                HasWebhookSecret = !string.IsNullOrEmpty(p.WebhookSecret),
                AllowedDomains = p.AllowedDomains,
                SystemPrompt = p.SystemPrompt
            });
        }

        [HttpPut("tenants/{tenantId}")]
        public async Task<IActionResult> UpdateTenant(Guid tenantId, UpdateTenantRequest model)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == tenantId && p.PartnerAccountId == partner.Id);
            if (project == null) return NotFound(new { message = "Tenant project not found." });

            // Validate tenant identifier uniqueness if provided and changed
            if (!string.IsNullOrEmpty(model.TenantIdentifier) && model.TenantIdentifier != project.TenantIdentifier)
            {
                var exists = await _db.Projects.AnyAsync(p => p.TenantIdentifier == model.TenantIdentifier && p.PartnerAccountId == partner.Id && p.Id != tenantId);
                if (exists)
                {
                    return BadRequest(new { message = $"Tenant identifier '{model.TenantIdentifier}' is already in use." });
                }
            }

            project.Name = model.TenantName;
            project.TenantIdentifier = model.TenantIdentifier;
            if (model.SystemPrompt != null) project.SystemPrompt = model.SystemPrompt;
            if (model.Provider != null) project.Provider = model.Provider;
            if (model.ModelName != null) project.ModelName = model.ModelName;
            project.AllowedDomains = model.AllowedDomains;
            project.WebhookUrl = model.WebhookUrl;
            
            if (model.WebhookSecret != null)
            {
                project.WebhookSecret = model.WebhookSecret;
            }

            await _db.SaveChangesAsync();

            // Also update the Default configuration's settings to stay in sync
            var defaultConfig = await _db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == project.Id && c.Name == "Default");
            if (defaultConfig != null)
            {
                if (model.SystemPrompt != null) defaultConfig.SystemPrompt = model.SystemPrompt;
                if (model.Provider != null) defaultConfig.DefaultProvider = model.Provider;
                if (model.ModelName != null) defaultConfig.DefaultModel = model.ModelName;
                await _db.SaveChangesAsync();
            }

            return NoContent();
        }

        [HttpPut("tenants/{tenantId}/embed-settings")]
        public async Task<IActionResult> UpdateTenantEmbedSettings(Guid tenantId, UpdateTenantEmbedSettingsRequest model)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == tenantId && p.PartnerAccountId == partner.Id);
            if (project == null) return NotFound(new { message = "Tenant project not found." });

            project.EmbedSettingsJson = model.EmbedSettingsJson;
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("tenants/{tenantId}")]
        public async Task<IActionResult> DeleteTenant(Guid tenantId)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == tenantId && p.PartnerAccountId == partner.Id);
            if (project == null) return NotFound(new { message = "Tenant project not found." });

            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("tenants/{tenantId}/token")]
        public async Task<ActionResult<TenantTokenResponse>> GenerateTenantToken(Guid tenantId)
        {
            var partner = await GetCurrentPartnerAsync();
            if (partner == null) return NotFound(new { message = "Partner account not found." });

            var project = await _db.Projects.AnyAsync(p => p.Id == tenantId && p.PartnerAccountId == partner.Id);
            if (!project) return NotFound(new { message = "Tenant project not found." });

            // Generate short-lived scoped token
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "super_secret_key_change_this_in_production_123456!!");

            var expiresAt = DateTime.UtcNow.AddHours(1);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, partner.OwnerId),
                    new Claim(ClaimTypes.Email, partner.Owner?.Email ?? ""),
                    new Claim(ClaimTypes.Role, "StandardUser"), // Render normal user UI but scoped
                    new Claim("project_scope", tenantId.ToString()),
                    new Claim("partner_id", partner.Id.ToString())
                }),
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenStr = tokenHandler.WriteToken(token);

            return Ok(new TenantTokenResponse
            {
                Token = tokenStr,
                ExpiresAt = expiresAt
            });
        }
    }
}
