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
    [Authorize(Policy = "AdminOnly")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController(
        ChatDbContext db,
        UserManager<ApplicationUser> userManager,
        ApiKeyService apiKeyService,
        IConfiguration configuration) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ApiKeyService _apiKeyService = apiKeyService;
        private readonly IConfiguration _configuration = configuration;

        [HttpGet("partners")]
        public async Task<ActionResult<IEnumerable<PartnerListItemDto>>> GetPartners()
        {
            var partners = await _db.PartnerAccounts
                .Include(pa => pa.Owner)
                .OrderByDescending(pa => pa.CreatedAt)
                .Select(pa => new PartnerListItemDto
                {
                    Id = pa.Id,
                    CompanyName = pa.CompanyName,
                    OwnerId = pa.OwnerId,
                    OwnerEmail = pa.Owner != null ? (pa.Owner.Email ?? string.Empty) : string.Empty,
                    TenantCount = _db.Projects.Count(p => p.PartnerAccountId == pa.Id),
                    MaxTenants = pa.MaxTenants,
                    CreditLimit = pa.CreditLimit,
                    CurrentSpend = pa.CurrentSpend,
                    MasterKeyActive = pa.MasterKeyActive,
                    AllowedDomainPattern = pa.AllowedDomainPattern,
                    CreatedAt = pa.CreatedAt
                })
                .ToListAsync();

            return Ok(partners);
        }

        [HttpPost("partners")]
        public async Task<IActionResult> CreatePartner(CreatePartnerDto model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            if (user.AccountType == UserRole.PartnerDeveloper && user.PartnerAccountId != null)
            {
                return BadRequest(new { message = "User is already a partner." });
            }

            var partner = new PartnerAccount
            {
                CompanyName = model.CompanyName,
                OwnerId = user.Id,
                AllowedDomainPattern = model.AllowedDomainPattern,
                MaxTenants = model.MaxTenants,
                CreditLimit = model.CreditLimit,
                MasterKeyHash = string.Empty,
                MasterKeyActive = true
            };

            _db.PartnerAccounts.Add(partner);
            await _db.SaveChangesAsync();

            if (user.AccountType != UserRole.SystemAdmin)
            {
                user.AccountType = UserRole.PartnerDeveloper;
            }
            user.PartnerAccountId = partner.Id;
            await _userManager.UpdateAsync(user);

            if (user.AccountType != UserRole.SystemAdmin)
            {
                if (!await _userManager.IsInRoleAsync(user, "PartnerDeveloper"))
                {
                    await _userManager.AddToRoleAsync(user, "PartnerDeveloper");
                }
                if (await _userManager.IsInRoleAsync(user, "StandardUser"))
                {
                    await _userManager.RemoveFromRoleAsync(user, "StandardUser");
                }
            }

            var rawKey = await _apiKeyService.GenerateMasterKeyAsync(partner.Id);

            var resultDto = new PartnerListItemDto
            {
                Id = partner.Id,
                CompanyName = partner.CompanyName,
                OwnerId = partner.OwnerId,
                OwnerEmail = user.Email ?? string.Empty,
                TenantCount = 0,
                MaxTenants = partner.MaxTenants,
                CreditLimit = partner.CreditLimit,
                CurrentSpend = partner.CurrentSpend,
                MasterKeyActive = partner.MasterKeyActive,
                AllowedDomainPattern = partner.AllowedDomainPattern,
                CreatedAt = partner.CreatedAt
            };

            return CreatedAtAction(nameof(GetPartners), null, new
            {
                partner = resultDto,
                masterKey = rawKey
            });
        }

        [HttpPut("partners/{id}")]
        public async Task<IActionResult> UpdatePartner(Guid id, UpdatePartnerDto model)
        {
            var partner = await _db.PartnerAccounts.FirstOrDefaultAsync(pa => pa.Id == id);
            if (partner == null) return NotFound(new { message = "Partner not found." });

            partner.CompanyName = model.CompanyName;
            partner.AllowedDomainPattern = model.AllowedDomainPattern;
            partner.MaxTenants = model.MaxTenants;
            partner.CreditLimit = model.CreditLimit;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("partners/{id}")]
        public async Task<IActionResult> DeletePartner(Guid id)
        {
            var partner = await _db.PartnerAccounts.FirstOrDefaultAsync(pa => pa.Id == id);
            if (partner == null) return NotFound();

            var user = await _userManager.FindByIdAsync(partner.OwnerId);
            if (user != null)
            {
                if (user.AccountType == UserRole.SystemAdmin)
                {
                    user.PartnerAccountId = null;
                    await _userManager.UpdateAsync(user);
                }
                else
                {
                    user.AccountType = UserRole.StandardUser;
                    user.PartnerAccountId = null;
                    await _userManager.UpdateAsync(user);

                    if (await _userManager.IsInRoleAsync(user, "PartnerDeveloper"))
                    {
                        await _userManager.RemoveFromRoleAsync(user, "PartnerDeveloper");
                    }
                    if (!await _userManager.IsInRoleAsync(user, "StandardUser"))
                    {
                        await _userManager.AddToRoleAsync(user, "StandardUser");
                    }
                }
            }

            _db.PartnerAccounts.Remove(partner);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> GetUsers()
        {
            var users = await _userManager.Users
                .Select(u => new UserListItemDto
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    Username = u.UserName ?? string.Empty,
                    Role = u.AccountType.ToString(),
                    ProjectCount = _db.Projects.Count(p => p.UserId == u.Id),
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> ChangeUserRole(string id, ChangeUserRoleDto model)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found." });

            if (!Enum.TryParse<UserRole>(model.Role, out var targetRole))
            {
                return BadRequest(new { message = "Invalid role specified." });
            }

            if (user.AccountType == targetRole)
            {
                return Ok();
            }

            var currentRoleStr = user.AccountType.ToString();
            if (await _userManager.IsInRoleAsync(user, currentRoleStr))
            {
                await _userManager.RemoveFromRoleAsync(user, currentRoleStr);
            }

            user.AccountType = targetRole;

            if (targetRole != UserRole.PartnerDeveloper && user.PartnerAccountId != null)
            {
                var partner = await _db.PartnerAccounts.FirstOrDefaultAsync(pa => pa.Id == user.PartnerAccountId);
                if (partner != null)
                {
                    _db.PartnerAccounts.Remove(partner);
                }
                user.PartnerAccountId = null;
            }

            await _userManager.UpdateAsync(user);
            await _userManager.AddToRoleAsync(user, targetRole.ToString());
            await _db.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("projects")]
        public async Task<ActionResult<IEnumerable<AdminProjectListItemDto>>> GetProjects()
        {
            var projects = await _db.Projects
                .Include(p => p.User)
                .Include(p => p.PartnerAccount)
                .Select(p => new AdminProjectListItemDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    OwnerEmail = p.User != null ? (p.User.Email ?? string.Empty) : string.Empty,
                    PartnerAccountId = p.PartnerAccountId,
                    PartnerCompanyName = p.PartnerAccount != null ? p.PartnerAccount.CompanyName : null,
                    TenantIdentifier = p.TenantIdentifier,
                    Provider = p.Provider,
                    ModelName = p.ModelName,
                    SessionCount = _db.ChatSessions.Count(s => s.ProjectId == p.Id),
                    MessageCount = _db.ChatMessages.Count(m => m.Session != null && m.Session.ProjectId == p.Id),
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return Ok(projects);
        }

        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics(int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);

            var totalUsers = await _db.Users.CountAsync();
            var totalPartners = await _db.PartnerAccounts.CountAsync();
            var totalProjects = await _db.Projects.CountAsync();
            var totalSessions = await _db.ChatSessions.CountAsync();

            var logsQuery = _db.AiRequestLogs.AsQueryable();
            var totalRequests = await logsQuery.CountAsync();
            var totalTokens = await logsQuery.SumAsync(l => (long)l.InputTokens + l.OutputTokens);
            var avgResponseMs = totalRequests > 0 ? await logsQuery.AverageAsync(l => l.DurationMs) : 0;
            var errorCount = await logsQuery.CountAsync(l => l.ErrorMessage != null);
            var errorRate = totalRequests > 0 ? Math.Round((double)errorCount / totalRequests * 100, 2) : 0;

            var volumePoints = await _db.AiRequestLogs
                .Where(l => l.CreatedAt >= since)
                .GroupBy(l => l.CreatedAt.Date)
                .Select(g => new PlatformVolumePointDto
                {
                    Date = g.Key,
                    Requests = g.Count(),
                    Sessions = _db.ChatSessions.Count(s => s.CreatedAt.Date == g.Key)
                })
                .OrderBy(p => p.Date)
                .ToListAsync();

            var providerStats = await _db.AiRequestLogs
                .GroupBy(l => l.Provider)
                .Select(g => new ProviderStatsDto
                {
                    Provider = g.Key ?? "Unknown",
                    Requests = g.Count()
                })
                .ToListAsync();

            return Ok(new
            {
                overview = new PlatformAnalyticsDto
                {
                    TotalUsers = totalUsers,
                    TotalPartners = totalPartners,
                    TotalProjects = totalProjects,
                    TotalSessions = totalSessions,
                    TotalRequests = totalRequests,
                    TotalTokens = totalTokens,
                    ErrorRate = errorRate,
                    AvgResponseMs = Math.Round(avgResponseMs, 1)
                },
                volume = volumePoints,
                providers = providerStats
            });
        }

        [HttpPost("impersonate/{userId}")]
        public async Task<IActionResult> Impersonate(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"] ?? "super_secret_key_change_this_in_production_123456!!");

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Email, user.Email!),
                    new Claim(ClaimTypes.Name, user.UserName ?? user.Email!),
                    new Claim(ClaimTypes.Role, user.AccountType.ToString()),
                    new Claim("partner_id", user.PartnerAccountId?.ToString() ?? ""),
                    new Claim("impersonated_by", User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenStr = tokenHandler.WriteToken(token);

            return Ok(new { token = tokenStr });
        }
    }
}
