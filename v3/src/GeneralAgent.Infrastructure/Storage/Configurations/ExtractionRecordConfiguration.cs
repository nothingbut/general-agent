using System.Text.Json;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// ExtractionRecord 实体配置
/// </summary>
public sealed class ExtractionRecordConfiguration : IEntityTypeConfiguration<ExtractionRecord>
{
    public void Configure(EntityTypeBuilder<ExtractionRecord> builder)
    {
        builder.ToTable("ExtractionRecords");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .IsRequired();

        builder.Property(e => e.Timestamp)
            .IsRequired();

        builder.Property(e => e.SessionId)
            .HasMaxLength(100);

        builder.Property(e => e.SkillName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.SkillNamespace)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Confidence)
            .IsRequired();

        builder.Property(e => e.Occurrences)
            .IsRequired();

        builder.Property(e => e.RejectionReason)
            .HasMaxLength(500);

        // 将 Metadata 字典序列化为 JSON 存储
        builder.Property(e => e.Metadata)
            .HasConversion(
                v => v != null ? JsonSerializer.Serialize(v, (JsonSerializerOptions?)null) : null,
                v => v != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) : null);

        // 索引
        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_ExtractionRecords_Timestamp");

        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("IX_ExtractionRecords_SessionId");

        builder.HasIndex(e => new { e.SkillNamespace, e.SkillName })
            .HasDatabaseName("IX_ExtractionRecords_Skill");

        builder.HasIndex(e => e.Action)
            .HasDatabaseName("IX_ExtractionRecords_Action");
    }
}
