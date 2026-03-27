using GeneralAgent.Infrastructure.Compression.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// CompressionHistory 实体配置
/// </summary>
internal sealed class CompressionHistoryConfiguration : IEntityTypeConfiguration<CompressionHistory>
{
    public void Configure(EntityTypeBuilder<CompressionHistory> builder)
    {
        builder.ToTable("compression_history");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.SessionId)
            .IsRequired();

        builder.Property(h => h.StrategyUsed)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(h => h.OriginalMessageCount)
            .IsRequired();

        builder.Property(h => h.CompressedMessageCount)
            .IsRequired();

        builder.Property(h => h.OriginalTokens)
            .IsRequired();

        builder.Property(h => h.CompressedTokens)
            .IsRequired();

        builder.Property(h => h.CompressionRatio)
            .IsRequired();

        builder.Property(h => h.DurationMs)
            .IsRequired();

        builder.Property(h => h.CompressedAt)
            .IsRequired();

        builder.Property(h => h.MetadataJson)
            .HasColumnType("TEXT");

        // 索引
        builder.HasIndex(h => h.SessionId);
        builder.HasIndex(h => h.CompressedAt);
        builder.HasIndex(h => h.StrategyUsed);
    }
}
