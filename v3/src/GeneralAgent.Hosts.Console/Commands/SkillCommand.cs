using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// skill 命令组 - 技能管理
/// </summary>
public static class SkillCommand
{
    /// <summary>
    /// 创建 skill 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("skill", "技能管理命令");

        // 添加子命令
        command.AddCommand(SkillListCommand.Create(serviceProvider));
        command.AddCommand(SkillInfoCommand.Create(serviceProvider));
        command.AddCommand(SkillRunCommand.Create(serviceProvider));

        return command;
    }
}
