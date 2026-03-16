using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// Session 实体配置
/// </summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .HasMaxLength(500);

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.ParentId);

        // 索引
        builder.HasIndex(s => s.CreatedAt);
        builder.HasIndex(s => s.UpdatedAt);
        builder.HasIndex(s => s.ParentId);
    }
}
