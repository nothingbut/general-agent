using System.CommandLine;
using System.IO;
using GeneralAgent.Application;
using GeneralAgent.Application.Services;
using GeneralAgent.Hosts.Console;
using GeneralAgent.Hosts.Console.Commands;
using GeneralAgent.Hosts.Console.Services;
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
    builder.Services.AddApplicationLayer(builder.Configuration);
    builder.Services.AddHostedService<BackgroundTaskService>();

    // 3. 注册 AgentRepl 和 Console 服务
    builder.Services.AddSingleton<AgentRepl>();
    builder.Services.AddScoped<ISearchService, SearchService>();
    builder.Services.AddScoped<SearchCommand>();
    builder.Services.AddScoped<TagCommand>();

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

    // 7. 初始化技能系统
    using (var scope = host.Services.CreateScope())
    {
        var skillService = scope.ServiceProvider.GetRequiredService<SkillService>();

        // 从配置文件读取技能目录
        var skillsDirectory = builder.Configuration["Skills:Directory"] ?? "../../../../../skills";

        // 如果是相对路径，转换为绝对路径
        if (!Path.IsPathRooted(skillsDirectory))
        {
            skillsDirectory = Path.Combine(AppContext.BaseDirectory, skillsDirectory);
            skillsDirectory = Path.GetFullPath(skillsDirectory);
        }

        var loadResult = await skillService.LoadSkillsAsync(skillsDirectory);
        if (!loadResult.IsSuccess)
        {
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine($"⚠️  技能加载失败: {loadResult.Error}");
            System.Console.ResetColor();
        }
        else
        {
            System.Console.WriteLine($"✅ 成功加载 {loadResult.Value} 个技能");
        }
    }

    // 8. 创建并执行命令
    var rootCommand = AgentRootCommand.Create(host.Services);
    return await rootCommand.InvokeAsync(args);
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
