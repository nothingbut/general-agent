using System.CommandLine;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// skill info 命令 - 显示技能详细信息
/// </summary>
public static class SkillInfoCommand
{
    /// <summary>
    /// 创建 skill info 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("info", "显示技能的详细信息");

        // 参数：技能名称
        var skillNameArgument = new Argument<string>(
            name: "skill-name",
            description: "技能名称（支持完整名称或简短名称）");
        command.AddArgument(skillNameArgument);

        command.SetHandler((skillName) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();

                // 尝试解析技能名称（支持 namespace:name 和 name 两种格式）
                var skill = ParseAndFindSkill(skillRegistry, skillName);

                if (skill == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 未找到技能: {skillName}[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]使用 'agent skill list' 查看所有可用技能[/]");
                    Environment.Exit(1);
                    return;
                }

                // 显示技能详情
                DisplaySkillInfo(skill);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 获取技能信息失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, skillNameArgument);

        return command;
    }

    /// <summary>
    /// 解析并查找技能
    /// </summary>
    private static GeneralAgent.Infrastructure.Skills.Models.Skill? ParseAndFindSkill(
        ISkillRegistry registry,
        string skillName)
    {
        // 1. 尝试完整名称匹配
        var skill = registry.GetByFullName(skillName);
        if (skill != null) return skill;

        // 2. 尝试解析 namespace:name 格式
        var parts = skillName.Split(':', 2);
        if (parts.Length == 2)
        {
            return registry.GetByName(parts[1], parts[0]);
        }

        // 3. 尝试只用名称查找
        return registry.GetByName(skillName);
    }

    /// <summary>
    /// 显示技能详细信息
    /// </summary>
    private static void DisplaySkillInfo(GeneralAgent.Infrastructure.Skills.Models.Skill skill)
    {
        // 标题
        var panel = new Panel($"[bold cyan]{skill.FullName}[/]")
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // 基本信息
        var grid = new Grid()
            .AddColumn()
            .AddColumn();

        grid.AddRow("[bold]名称:[/]", skill.Name);

        if (!string.IsNullOrEmpty(skill.Namespace))
        {
            grid.AddRow("[bold]命名空间:[/]", skill.Namespace);
        }

        grid.AddRow("[bold]描述:[/]", skill.Description);
        grid.AddRow("[bold]需要上下文:[/]", skill.RequiresContext ? "[green]是[/]" : "[dim]否[/]");
        grid.AddRow("[bold]返回给 LLM:[/]", skill.ReturnToLLM ? "[green]是[/]" : "[dim]否[/]");

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        // 参数列表
        if (skill.Parameters.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold underline]参数:[/]");
            AnsiConsole.WriteLine();

            var paramsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("参数名")
                .AddColumn("类型")
                .AddColumn("必填")
                .AddColumn("默认值")
                .AddColumn("描述");

            foreach (var param in skill.Parameters)
            {
                paramsTable.AddRow(
                    $"[cyan]{param.Name}[/]",
                    param.Type,
                    param.Required ? "[green]✓[/]" : "[dim]-[/]",
                    param.DefaultValue?.ToString() ?? "[dim]-[/]",
                    param.Description ?? "[dim]无描述[/]"
                );
            }

            AnsiConsole.Write(paramsTable);
            AnsiConsole.WriteLine();
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]此技能不需要参数[/]");
            AnsiConsole.WriteLine();
        }

        // 标签
        if (skill.Tags != null && skill.Tags.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold underline]标签:[/]");
            foreach (var tag in skill.Tags)
            {
                AnsiConsole.MarkupLine($"  [dim]{tag.Key}:[/] {tag.Value}");
            }
            AnsiConsole.WriteLine();
        }

        // 模板预览
        AnsiConsole.MarkupLine("[bold underline]提示词模板:[/]");
        var templatePanel = new Panel(skill.Template)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey)
        };
        AnsiConsole.Write(templatePanel);
        AnsiConsole.WriteLine();

        // 使用示例
        AnsiConsole.MarkupLine("[bold underline]使用示例:[/]");
        var exampleCommand = BuildExampleCommand(skill);
        AnsiConsole.MarkupLine($"  [dim]$[/] [cyan]agent skill run {exampleCommand}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// 构建示例命令
    /// </summary>
    private static string BuildExampleCommand(GeneralAgent.Infrastructure.Skills.Models.Skill skill)
    {
        var parts = new List<string> { skill.FullName };

        foreach (var param in skill.Parameters.Where(p => p.Required))
        {
            var exampleValue = param.Type.ToLower() switch
            {
                "string" => $"\"{param.Name}_value\"",
                "int" => "123",
                "bool" => "true",
                "array" => "[\"item1\", \"item2\"]",
                _ => "value"
            };
            parts.Add($"--{param.Name} {exampleValue}");
        }

        return string.Join(" ", parts);
    }
}
