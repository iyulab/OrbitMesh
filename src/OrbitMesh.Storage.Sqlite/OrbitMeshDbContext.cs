using Microsoft.EntityFrameworkCore;
using OrbitMesh.Storage.Sqlite.Entities;

namespace OrbitMesh.Storage.Sqlite;

/// <summary>
/// EF Core DbContext for OrbitMesh storage.
/// </summary>
public sealed class OrbitMeshDbContext : DbContext
{
    public OrbitMeshDbContext(DbContextOptions<OrbitMeshDbContext> options)
        : base(options)
    {
    }

    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<WorkflowDefinitionEntity> WorkflowDefinitions => Set<WorkflowDefinitionEntity>();
    public DbSet<WorkflowInstanceEntity> WorkflowInstances => Set<WorkflowInstanceEntity>();
    public DbSet<EventEntity> Events => Set<EventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Job configuration
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedAgentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => e.Command);

            entity.HasOne(e => e.AssignedAgent)
                .WithMany(a => a.Jobs)
                .HasForeignKey(e => e.AssignedAgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Agent configuration
        modelBuilder.Entity<AgentEntity>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Group);
            entity.HasIndex(e => e.ConnectionId);
            entity.HasIndex(e => e.LastHeartbeat);
        });

        // WorkflowDefinition configuration
        modelBuilder.Entity<WorkflowDefinitionEntity>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => new { e.Name, e.Version }).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });

        // WorkflowInstance configuration
        modelBuilder.Entity<WorkflowInstanceEntity>(entity =>
        {
            entity.HasIndex(e => e.WorkflowId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });

            entity.HasOne(e => e.WorkflowDefinition)
                .WithMany(w => w.Instances)
                .HasForeignKey(e => e.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Event configuration
        modelBuilder.Entity<EventEntity>(entity =>
        {
            entity.HasIndex(e => e.StreamId);
            entity.HasIndex(e => new { e.StreamId, e.Version }).IsUnique();
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
