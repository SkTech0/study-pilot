using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyPilot.Infrastructure.Persistence;

namespace StudyPilot.Infrastructure.Persistence.Configurations;

public sealed class ChatMessageCitationConfiguration : IEntityTypeConfiguration<ChatMessageCitation>
{
    public void Configure(EntityTypeBuilder<ChatMessageCitation> builder)
    {
        builder.ToTable("ChatMessageCitations");
        builder.HasKey(x => new { x.MessageId, x.ChunkId });

        builder.HasOne<Domain.Entities.ChatMessage>()
            .WithMany()
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.DocumentChunk>()
            .WithMany()
            .HasForeignKey(x => x.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ChunkId);
    }
}

