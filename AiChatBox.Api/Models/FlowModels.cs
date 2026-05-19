using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace AiChatBox.Api.Models
{
    public class ConversationFlow
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ProjectId { get; set; }
        public virtual Project Project { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Keyword or intent that triggers this flow.
        /// </summary>
        [MaxLength(100)]
        public string? TriggerKeyword { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<FlowNode> Nodes { get; set; } = new List<FlowNode>();
        public virtual ICollection<FlowEdge> Edges { get; set; } = new List<FlowEdge>();
    }

    public class FlowNode
    {
        [Key]
        [MaxLength(100)]
        public string Id { get; set; } = string.Empty; // Using string to support Vue Flow UUIDs

        public Guid FlowId { get; set; }
        public virtual ConversationFlow Flow { get; set; } = null!;

        /// <summary>
        /// e.g. "trigger", "message", "input", "ai", "webhook"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// JSON blob containing node configuration (e.g. text for message, url for webhook).
        /// </summary>
        public string DataJson { get; set; } = "{}";

        // UI Position
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    public class FlowEdge
    {
        [Key]
        [MaxLength(100)]
        public string Id { get; set; } = string.Empty; // Using string to support Vue Flow UUIDs

        public Guid FlowId { get; set; }
        public virtual ConversationFlow Flow { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string SourceNodeId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TargetNodeId { get; set; } = string.Empty;

        /// <summary>
        /// Optional condition for evaluating edge traversal (e.g., specific user intent, or fallback).
        /// </summary>
        [MaxLength(200)]
        public string? Condition { get; set; }
    }

    public class FlowExecutionLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid FlowId { get; set; }
        public virtual ConversationFlow Flow { get; set; } = null!;

        public Guid SessionId { get; set; }
        public virtual ChatSession Session { get; set; } = null!;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// JSON array of executed steps telemetry.
        /// Each step details: node id, node type, variables snapshot, duration, status, inputs.
        /// </summary>
        public string StepsJson { get; set; } = "[]";
    }
}
