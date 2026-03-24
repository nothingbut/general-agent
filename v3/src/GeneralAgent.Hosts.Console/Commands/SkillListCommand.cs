using System.CommandLine;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// skill list 命令 - 列出所有技能
/// </summary>
public static class SkillListCommand
{
    /// <summary>
    /// 创建 skill list 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("list", "列出所有已注册的技能");

        // 选项：命名空间过滤
        var namespaceOption = new Option<string?>(
            aliases: new[] { "--namespace", "-n" },
            description: "按命名空间过滤技能");
        command.AddOption(namespaceOption);

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler((namespaceName, format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();

                // 获取技能列表
                var skills = string.IsNullOrEmpty(namespaceName)
                    ? skillRegistry.GetAllSkills()
                    : skillRegistry.GetSkillsByNamespace(namespaceName);

                if (skills.Count == 0)
                {
                    var message = string.IsNullOrEmpty(namespaceName)
                        ? "[yellow]没有已注册的技能[/]"
                        : $"[yellow]命名空间 '{namespaceName}' 下没有技能[/]";
                    AnsiConsole.MarkupLine(message);
                    return;
                }

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var skillsData = skills.Select(s => new
                    {
                        s.Name,
                        s.Namespace,
                        FullName = s.FullName,
                        s.Description,
                        ParameterCount = s.Parameters.Count,
                        s.RequiresContext
                    }).ToList();

                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Total = skills.Count,
                        Skills = skillsData
                    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("完整名称")
                        .AddColumn("命名空间")
                        .AddColumn("描述")
                        .AddColumn("参数数量")
                        .AddColumn("需要上下文");

                    foreach (var skill in skills.OrderBy(s => s.FullName))
                    {
                        table.AddRow(
                            $"[cyan]{skill.FullName}[/]",
                            skill.Namespace ?? "[dim]无[/]",
                            TruncateDescription(skill.Description, 40),
                            skill.Parameters.Count.ToString(),
                            skill.RequiresContext ? "[green]✓[/]" : "[dim]-[/]"
                        );
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.MarkupLine($"\n共找到 [cyan]{skills.Count}[/] 个技能");

                    // 显示使用提示
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]使用 'agent skill info <技能名>' 查看技能详情[/]");
                    AnsiConsole.MarkupLine("[dim]使用 'agent skill run <技能名> [参数...]' 执行技能[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 列出技能失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, namespaceOption, formatOption);

        return command;
    }

    /// <summary>
    /// 截断描述文本
    /// </summary>
    private static string TruncateDescription(string description, int maxLength)
    {
        if (string.IsNullOrEmpty(description))
            return "[dim]无描述[/]";

        // 移除换行符
        description = description.Replace("\n", " ").Replace("\r", " ");

        if (description.Length <= maxLength)
            return description;

        return description.Substring(0, maxLength - 3) + "...";
    }
}
