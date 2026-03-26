using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// SessionTag 实体配置
/// </summary>
internal sealed class SessionTagConfiguration : IEntityTypeConfiguration<SessionTag>
{
    public void Configure(EntityTypeBuilder<SessionTag> builder)
    {
        builder.ToTable("session_tags");

        // 复合主键：SessionId + Tag
        builder.HasKey(t => new { t.SessionId, t.Tag });

        builder.Property(t => t.SessionId)
            .IsRequired();

        builder.Property(t => t.Tag)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Color)
            .HasMaxLength(20);

        builder.Property(t => t.Emoji)
            .HasMaxLength(10);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.Source)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // 外键关系
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // 索引
        builder.HasIndex(t => t.Tag); // 按标签查找会话
    }
}
