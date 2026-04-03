using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// 根命令 - 默认启动 REPL
/// </summary>
public static class AgentRootCommand
{
    /// <summary>
    /// 创建根命令
    /// </summary>
    public static RootCommand Create(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("General Agent V3 - AI 对话助手")
        {
            Name = "agent"
        };

        // 添加全局选项
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "显示详细输出");
        rootCommand.AddGlobalOption(verboseOption);

        // 添加子命令
        rootCommand.AddCommand(NewCommand.Create(serviceProvider));
        rootCommand.AddCommand(ListCommand.Create(serviceProvider));
        rootCommand.AddCommand(ChatCommand.Create(serviceProvider));
        rootCommand.AddCommand(SwitchCommand.Create(serviceProvider));
        rootCommand.AddCommand(DeleteCommand.Create(serviceProvider));
        rootCommand.AddCommand(ExportCommand.Create(serviceProvider));
        rootCommand.AddCommand(SkillCommand.Create(serviceProvider));
        rootCommand.AddCommand(ConfigCommand.Create(serviceProvider));
        rootCommand.AddCommand(FileCommand.Create(serviceProvider));

        // 默认行为：启动 REPL
        rootCommand.SetHandler(async (verbose) =>
        {
            var repl = serviceProvider.GetRequiredService<AgentRepl>();
            await repl.RunAsync();
        }, verboseOption);

        return rootCommand;
    }
}
