using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class KnowledgePipelineHeartbeatConfiguration : IEntityTypeConfiguration<KnowledgePipelineHeartbeat>
{
    public void Configure(EntityTypeBuilder<KnowledgePipelineHeartbeat> builder)
    {
        builder.ToTable("KnowledgePipelineHeartbeats");
        builder.HasKey(h => h.InstanceId);
        builder.Property(h => h.InstanceId).HasMaxLength(128);
        builder.Property(h => h.LastSeenUtc).HasConversion(static v => v, static v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(h => h.CurrentMode);
        builder.Property(h => h.OutboxPending);
        builder.Property(h => h.EmbeddingDepth);
        builder.Property(h => h.AILimiterWaiters);
    }
}
