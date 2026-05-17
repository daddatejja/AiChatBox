using System.ComponentModel.DataAnnotations;

namespace AiChatBox.Api.DTOs
{
    public class ConversationFlowDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TriggerKeyword { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<FlowNodeDto> Nodes { get; set; } = new();
        public List<FlowEdgeDto> Edges { get; set; } = new();
    }

    public class FlowNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DataJson { get; set; } = "{}";
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    public class FlowEdgeDto
    {
        public string Id { get; set; } = string.Empty;
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string? Condition { get; set; }
    }

    public class UpdateConversationFlowDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [MaxLength(100)]
        public string? TriggerKeyword { get; set; }
        
        public bool IsActive { get; set; }
        
        public List<FlowNodeDto> Nodes { get; set; } = new();
        public List<FlowEdgeDto> Edges { get; set; } = new();
    }
}
