using System.Text.Json;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Compression.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GeneralAgent.Infrastructure.Storage;

/// <summary>
/// Agent 数据库上下文
/// </summary>
public sealed class AgentDbContext : DbContext
{
    public AgentDbContext(DbContextOptions<AgentDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 会话集合
    /// </summary>
    public DbSet<Session> Sessions => Set<Session>();

    /// <summary>
    /// 消息集合
    /// </summary>
    public DbSet<Message> Messages => Set<Message>();

    /// <summary>
    /// 会话标签集合
    /// </summary>
    public DbSet<SessionTag> SessionTags => Set<SessionTag>();

    /// <summary>
    /// 压缩历史记录集合
    /// </summary>
    public DbSet<CompressionHistory> CompressionHistories => Set<CompressionHistory>();

    /// <summary>
    /// 压缩配置集合
    /// </summary>
    public DbSet<CompressionConfig> CompressionConfigs => Set<CompressionConfig>();

    /// <summary>
    /// 技能提取历史记录集合
    /// </summary>
    public DbSet<ExtractionRecord> ExtractionRecords => Set<ExtractionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用所有配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);

        // 配置 Message.Metadata 的 value comparer
        modelBuilder.Entity<Message>()
            .Property(m => m.Metadata)
            .Metadata.SetValueComparer(
                new ValueComparer<Dictionary<string, JsonElement>?>(
                    (c1, c2) => JsonSerializer.Serialize(c1) == JsonSerializer.Serialize(c2),
                    c => c == null ? 0 : JsonSerializer.Serialize(c).GetHashCode(),
                    c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(c))
                )
            );

        // 配置 ExtractionRecord.Metadata 的 value comparer
        modelBuilder.Entity<ExtractionRecord>()
            .Property(e => e.Metadata)
            .Metadata.SetValueComparer(
                new ValueComparer<Dictionary<string, object>?>(
                    (c1, c2) => JsonSerializer.Serialize(c1) == JsonSerializer.Serialize(c2),
                    c => c == null ? 0 : JsonSerializer.Serialize(c).GetHashCode(),
                    c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(c))
                )
            );
    }
}
