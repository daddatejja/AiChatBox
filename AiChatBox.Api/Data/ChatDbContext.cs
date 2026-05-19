using AiChatBox.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AiChatBox.Api.Data
{
    public class ChatDbContext : IdentityDbContext<ApplicationUser>
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectConfiguration> Configurations { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<CustomTool> CustomTools { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<AiRequestLog> AiRequestLogs { get; set; }
        public DbSet<UploadedFile> UploadedFiles { get; set; }
        public DbSet<ConfigurationHistory> ConfigurationHistories { get; set; }
        public DbSet<KnowledgeDocument> KnowledgeDocuments { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<ProjectDatabase> ProjectDatabases { get; set; }
        public DbSet<WebsiteCrawlJob> WebsiteCrawlJobs { get; set; }
        public DbSet<ConversationRule> ConversationRules { get; set; }
        
        public DbSet<ConversationFlow> ConversationFlows { get; set; }
        public DbSet<FlowNode> FlowNodes { get; set; }
        public DbSet<FlowEdge> FlowEdges { get; set; }
        public DbSet<FlowExecutionLog> FlowExecutionLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.HasPostgresExtension("vector");

            modelBuilder.Entity<FlowExecutionLog>()
                .HasOne(l => l.Flow)
                .WithMany()
                .HasForeignKey(l => l.FlowId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FlowExecutionLog>()
                .HasOne(l => l.Session)
                .WithMany()
                .HasForeignKey(l => l.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Database)
                .WithOne(d => d.Project)
                .HasForeignKey<ProjectDatabase>(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.WebsiteCrawlJobs)
                .WithOne(j => j.Project)
                .HasForeignKey(j => j.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.ApiKeys)
                .WithOne(k => k.Project)
                .HasForeignKey(k => k.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Configurations)
                .WithOne(c => c.Project)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectConfiguration>()
                .HasMany(c => c.ApiKeys)
                .WithOne(k => k.Configuration)
                .HasForeignKey(k => k.ConfigurationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProjectConfiguration>()
                .HasMany(c => c.Sessions)
                .WithOne(s => s.Configuration)
                .HasForeignKey(s => s.ConfigurationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.CustomTools)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatSession>()
                .HasMany(s => s.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Link Session to Project
            modelBuilder.Entity<ChatSession>()
                .HasOne(s => s.Project)
                .WithMany(p => p.Sessions)
                .HasForeignKey(s => s.ProjectId)
                .IsRequired(false);

            // Flow Relationships
            modelBuilder.Entity<ConversationFlow>()
                .HasMany(f => f.Nodes)
                .WithOne(n => n.Flow)
                .HasForeignKey(n => n.FlowId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConversationFlow>()
                .HasMany(f => f.Edges)
                .WithOne(e => e.Flow)
                .HasForeignKey(e => e.FlowId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatSession>()
                .HasOne(s => s.ActiveFlow)
                .WithMany()
                .HasForeignKey(s => s.ActiveFlowId)
                .OnDelete(DeleteBehavior.SetNull);

            // Knowledge Base
            modelBuilder.Entity<KnowledgeDocument>()
                .HasMany(d => d.Chunks)
                .WithOne(c => c.Document)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.KnowledgeDocuments)
                .WithOne(d => d.Project)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.ConversationRules)
                .WithOne(r => r.Project)
                .HasForeignKey(r => r.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ConfigurationHistory>()
                .HasOne(h => h.Configuration)
                .WithMany()
                .HasForeignKey(h => h.ConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DocumentChunk>()
                .Property(c => c.Embedding)
                .HasColumnType("vector(3072)");
        }
    }
}
