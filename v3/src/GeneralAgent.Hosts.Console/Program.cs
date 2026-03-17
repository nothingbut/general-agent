using GeneralAgent.Application;
using GeneralAgent.Hosts.Console;
using GeneralAgent.Infrastructure;
using GeneralAgent.Infrastructure.LLM;
using GeneralAgent.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// 创建 Host Builder
var builder = Host.CreateApplicationBuilder(args);

// 配置服务
try
{
    // 1. 配置数据库连接
    var connectionString = builder.Configuration.GetConnectionString("AgentDb")
        ?? throw new InvalidOperationException("未找到数据库连接字符串 'AgentDb'");

    // 2. 注册各层服务
    builder.Services.AddInfrastructure(connectionString);
    builder.Services.AddLLMInfrastructure(builder.Configuration);
    builder.Services.AddApplicationLayer();

    // 3. 注册 AgentRepl
    builder.Services.AddSingleton<AgentRepl>();

    // 4. 配置日志
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Error;
    });
    builder.Logging.SetMinimumLevel(LogLevel.Warning); // 只显示警告和错误

    var host = builder.Build();

    // 5. 自动应用数据库迁移
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    // 运行 REPL
    var repl = host.Services.GetRequiredService<AgentRepl>();
    await repl.RunAsync();

    return 0;
}
catch (Exception ex)
{
    System.Console.ForegroundColor = ConsoleColor.Red;
    System.Console.Error.WriteLine($"启动失败: {ex.Message}");
    System.Console.ResetColor();

    if (ex.InnerException != null)
    {
        System.Console.Error.WriteLine($"内部错误: {ex.InnerException.Message}");
    }

    return 1;
}
