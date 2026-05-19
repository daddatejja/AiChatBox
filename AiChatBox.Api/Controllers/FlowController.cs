using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/projects/{projectId}/flows")]
    public class FlowController(ChatDbContext db, ILogger<FlowController> logger) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly ILogger<FlowController> _logger = logger;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConversationFlowDto>>> GetFlows(Guid projectId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var flows = await _db.ConversationFlows
                .Where(f => f.ProjectId == projectId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new ConversationFlowDto
                {
                    Id = f.Id,
                    ProjectId = f.ProjectId,
                    Name = f.Name,
                    Description = f.Description,
                    TriggerKeyword = f.TriggerKeyword,
                    IsActive = f.IsActive,
                    CreatedAt = f.CreatedAt
                })
                .ToListAsync();

            return Ok(flows);
        }

        [HttpGet("{flowId}")]
        public async Task<ActionResult<ConversationFlowDto>> GetFlow(Guid projectId, Guid flowId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var flow = await _db.ConversationFlows
                .Include(f => f.Nodes)
                .Include(f => f.Edges)
                .FirstOrDefaultAsync(f => f.Id == flowId && f.ProjectId == projectId);

            if (flow == null) return NotFound("Flow not found");

            return Ok(new ConversationFlowDto
            {
                Id = flow.Id,
                ProjectId = flow.ProjectId,
                Name = flow.Name,
                Description = flow.Description,
                TriggerKeyword = flow.TriggerKeyword,
                IsActive = flow.IsActive,
                CreatedAt = flow.CreatedAt,
                Nodes = flow.Nodes.Select(n => new FlowNodeDto
                {
                    Id = n.Id,
                    Type = n.Type,
                    DataJson = n.DataJson,
                    PositionX = n.PositionX,
                    PositionY = n.PositionY
                }).ToList(),
                Edges = flow.Edges.Select(e => new FlowEdgeDto
                {
                    Id = e.Id,
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    Condition = e.Condition
                }).ToList()
            });
        }

        [HttpPost]
        public async Task<ActionResult<ConversationFlowDto>> CreateFlow(Guid projectId, [FromBody] UpdateConversationFlowDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var flow = new ConversationFlow
            {
                ProjectId = projectId,
                Name = dto.Name,
                Description = dto.Description,
                TriggerKeyword = dto.TriggerKeyword,
                IsActive = dto.IsActive
            };

            foreach (var node in dto.Nodes)
            {
                flow.Nodes.Add(new FlowNode
                {
                    Id = node.Id,
                    Type = node.Type,
                    DataJson = node.DataJson,
                    PositionX = node.PositionX,
                    PositionY = node.PositionY
                });
            }

            foreach (var edge in dto.Edges)
            {
                flow.Edges.Add(new FlowEdge
                {
                    Id = edge.Id,
                    SourceNodeId = edge.SourceNodeId,
                    TargetNodeId = edge.TargetNodeId,
                    Condition = edge.Condition
                });
            }

            _db.ConversationFlows.Add(flow);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFlow), new { projectId, flowId = flow.Id }, new ConversationFlowDto { Id = flow.Id });
        }

        [HttpPut("{flowId}")]
        public async Task<IActionResult> UpdateFlow(Guid projectId, Guid flowId, [FromBody] UpdateConversationFlowDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var flow = await _db.ConversationFlows
                .Include(f => f.Nodes)
                .Include(f => f.Edges)
                .FirstOrDefaultAsync(f => f.Id == flowId && f.ProjectId == projectId);

            if (flow == null) return NotFound("Flow not found");

            flow.Name = dto.Name;
            flow.Description = dto.Description;
            flow.TriggerKeyword = dto.TriggerKeyword;
            flow.IsActive = dto.IsActive;

            // Simple update strategy: replace all nodes and edges
            _db.FlowNodes.RemoveRange(flow.Nodes);
            _db.FlowEdges.RemoveRange(flow.Edges);

            flow.Nodes = dto.Nodes.Select(n => new FlowNode
            {
                Id = n.Id,
                Type = n.Type,
                DataJson = n.DataJson,
                PositionX = n.PositionX,
                PositionY = n.PositionY
            }).ToList();

            flow.Edges = dto.Edges.Select(e => new FlowEdge
            {
                Id = e.Id,
                SourceNodeId = e.SourceNodeId,
                TargetNodeId = e.TargetNodeId,
                Condition = e.Condition
            }).ToList();

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{flowId}")]
        public async Task<IActionResult> DeleteFlow(Guid projectId, Guid flowId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var flow = await _db.ConversationFlows.FirstOrDefaultAsync(f => f.Id == flowId && f.ProjectId == projectId);
            if (flow == null) return NotFound();

            _db.ConversationFlows.Remove(flow);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("execution-logs")]
        public async Task<IActionResult> GetExecutionLogs(
            Guid projectId, 
            [FromQuery] Guid? flowId = null, 
            [FromQuery] Guid? sessionId = null, 
            [FromQuery] int offset = 0, 
            [FromQuery] int limit = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var query = _db.FlowExecutionLogs
                .Where(l => l.Flow.ProjectId == projectId);

            if (flowId.HasValue)
            {
                query = query.Where(l => l.FlowId == flowId.Value);
            }

            if (sessionId.HasValue)
            {
                query = query.Where(l => l.SessionId == sessionId.Value);
            }

            var total = await query.CountAsync();
            var logsList = await query
                .Include(l => l.Flow)
                .Include(l => l.Session)
                .OrderByDescending(l => l.StartedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            var dtos = logsList.Select(l => {
                int stepsCount = 0;
                double totalDuration = 0;
                try
                {
                    var steps = JsonSerializer.Deserialize<List<FlowStepTelemetry>>(l.StepsJson);
                    if (steps != null)
                    {
                        stepsCount = steps.Count;
                        totalDuration = steps.Sum(s => s.DurationMs);
                    }
                }
                catch {}

                return new FlowExecutionLogDto
                {
                    Id = l.Id,
                    FlowId = l.FlowId,
                    FlowName = l.Flow.Name,
                    SessionId = l.SessionId,
                    SessionTitle = l.Session.Title ?? l.SessionId.ToString(),
                    StartedAt = l.StartedAt,
                    CompletedAt = l.CompletedAt,
                    StepsCount = stepsCount,
                    TotalDurationMs = totalDuration
                };
            }).ToList();

            return Ok(new { items = dtos, total });
        }

        [HttpGet("execution-logs/{logId}")]
        public async Task<IActionResult> GetExecutionLogDetail(Guid projectId, Guid logId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
            if (project == null) return NotFound("Project not found");

            var log = await _db.FlowExecutionLogs
                .Include(l => l.Flow)
                .Include(l => l.Session)
                .FirstOrDefaultAsync(l => l.Id == logId && l.Flow.ProjectId == projectId);

            if (log == null) return NotFound("Execution log not found");

            var dto = new FlowExecutionLogDetailDto
            {
                Id = log.Id,
                FlowId = log.FlowId,
                FlowName = log.Flow.Name,
                SessionId = log.SessionId,
                SessionTitle = log.Session.Title ?? log.SessionId.ToString(),
                StartedAt = log.StartedAt,
                CompletedAt = log.CompletedAt,
                StepsJson = log.StepsJson
            };

            return Ok(dto);
        }
    }
}
