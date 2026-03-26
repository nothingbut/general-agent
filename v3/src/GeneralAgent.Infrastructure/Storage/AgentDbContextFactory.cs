using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GeneralAgent.Infrastructure.Storage;

/// <summary>
/// EF Core 设计时 DbContext 工厂
/// 用于生成迁移和更新数据库
/// </summary>
public sealed class AgentDbContextFactory : IDesignTimeDbContextFactory<AgentDbContext>
{
    public AgentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgentDbContext>();

        // 使用默认数据库路径（用户主目录下的 .agent/agent.db）
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var agentDir = Path.Combine(homeDir, ".agent");
        var dbPath = Path.Combine(agentDir, "agent.db");

        // 确保目录存在
        Directory.CreateDirectory(agentDir);

        // 配置 SQLite
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new AgentDbContext(optionsBuilder.Options);
    }
}
