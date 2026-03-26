using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用所有配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);
    }
}
