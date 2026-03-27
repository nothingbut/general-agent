using GeneralAgent.Infrastructure.Compression.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// CompressionConfig 实体配置
/// </summary>
internal sealed class CompressionConfigConfiguration : IEntityTypeConfiguration<CompressionConfig>
{
    public void Configure(EntityTypeBuilder<CompressionConfig> builder)
    {
        builder.ToTable("compression_configs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.SessionId)
            .IsRequired();

        builder.Property(c => c.AutoCompressionEnabled)
            .IsRequired();

        builder.Property(c => c.AutoCompressionThreshold)
            .IsRequired();

        builder.Property(c => c.DefaultStrategy)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.StrategyOptionsJson)
            .HasColumnType("TEXT");

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();

        // 索引：每个会话只能有一个配置
        builder.HasIndex(c => c.SessionId)
            .IsUnique();

        builder.HasIndex(c => c.CreatedAt);
    }
}
