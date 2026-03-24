using System.CommandLine;
using GeneralAgent.Application.Services;
using GeneralAgent.Hosts.Console.Utils;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// skill run 命令 - 执行技能
/// </summary>
public static class SkillRunCommand
{
    /// <summary>
    /// 创建 skill run 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("run", "执行指定的技能");

        // 参数：技能名称
        var skillNameArgument = new Argument<string>(
            name: "skill-name",
            description: "技能名称（支持完整名称或简短名称）");
        command.AddArgument(skillNameArgument);

        // 选项：会话 ID（可选，如果不提供则创建临时会话）
        var sessionIdOption = new Option<string?>(
            aliases: new[] { "--session", "-s" },
            description: "会话 ID（可选，默认创建临时会话）");
        command.AddOption(sessionIdOption);

        // 选项：提供商
        var providerOption = new Option<string?>(
            aliases: new[] { "--provider", "-p" },
            description: "LLM 提供商（默认使用配置文件中的默认提供商）");
        command.AddOption(providerOption);

        // 选项：流式输出
        var streamOption = new Option<bool>(
            aliases: new[] { "--stream" },
            getDefaultValue: () => true,
            description: "启用流式输出");
        command.AddOption(streamOption);

        // 参数：技能参数（key=value 格式）
        var argsArgument = new Argument<string[]>(
            name: "args",
            description: "技能参数（key=value 格式）")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        command.AddArgument(argsArgument);

        command.SetHandler(async (skillName, sessionIdStr, provider, stream, args) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var skillRegistry = scope.ServiceProvider.GetRequiredService<ISkillRegistry>();
                var skillExecutor = scope.ServiceProvider.GetRequiredService<ISkillExecutor>();
                var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                // 1. 查找技能
                var skill = ParseAndFindSkill(skillRegistry, skillName);
                if (skill == null)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 未找到技能: {skillName}[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]使用 'agent skill list' 查看所有可用技能[/]");
                    Environment.Exit(1);
                    return;
                }

                // 2. 解析参数
                var parseResult = SkillArgumentParser.Parse(skill, args);
                if (!parseResult.IsSuccess)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 参数解析失败:[/]");
                    AnsiConsole.MarkupLine($"[red]{parseResult.Error}[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]使用格式:[/] agent skill run {0} {1}",
                        skill.FullName,
                        SkillArgumentParser.BuildUsageHint(skill));
                    AnsiConsole.MarkupLine("[dim]查看详情:[/] agent skill info {0}", skill.FullName);
                    Environment.Exit(1);
                    return;
                }

                var arguments = parseResult.Value!;

                // 3. 获取或创建会话
                Guid sessionId;
                bool isTemporarySession = false;

                if (!string.IsNullOrEmpty(sessionIdStr))
                {
                    // 使用指定的会话
                    if (Guid.TryParse(sessionIdStr, out var fullId))
                    {
                        sessionId = fullId;
                    }
                    else
                    {
                        // 尝试通过前缀匹配
                        var sessions = await sessionService.ListSessionsAsync(limit: 100);
                        var matchedSession = sessions.Items.FirstOrDefault(s =>
                            s.Id.ToString().StartsWith(sessionIdStr, StringComparison.OrdinalIgnoreCase));

                        if (matchedSession == null)
                        {
                            AnsiConsole.MarkupLine($"[red]✗ 未找到会话: {sessionIdStr}[/]");
                            Environment.Exit(1);
                            return;
                        }

                        sessionId = matchedSession.Id;
                    }

                    // 验证会话存在
                    var session = await sessionService.GetSessionAsync(sessionId);
                    if (session == null)
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 会话不存在: {sessionId}[/]");
                        Environment.Exit(1);
                        return;
                    }
                }
                else
                {
                    // 创建临时会话
                    var tempSession = await sessionService.CreateSessionAsync($"临时会话 - {skill.Name}");
                    sessionId = tempSession.Id;
                    isTemporarySession = true;
                }

                // 4. 确定提供商
                var currentProvider = provider ?? configuration["LLM:DefaultProvider"] ?? "Ollama";

                // 5. 显示执行信息
                AnsiConsole.MarkupLine($"[dim]技能: {skill.FullName}[/]");
                AnsiConsole.MarkupLine($"[dim]提供商: {currentProvider}[/]");
                if (!isTemporarySession)
                {
                    AnsiConsole.MarkupLine($"[dim]会话: {sessionId.ToString()[..8]}...[/]");
                }
                AnsiConsole.WriteLine();

                // 显示参数
                if (arguments.Count > 0)
                {
                    AnsiConsole.MarkupLine("[dim]参数:[/]");
                    foreach (var (key, value) in arguments)
                    {
                        AnsiConsole.MarkupLine($"[dim]  {key}:[/] {value?.ToString() ?? "null"}");
                    }
                    AnsiConsole.WriteLine();
                }

                // 6. 执行技能
                AnsiConsole.MarkupLine("[bold green]执行结果>[/]");

                if (stream)
                {
                    // 流式输出
                    await foreach (var content in skillExecutor.ExecuteStreamAsync(
                        skill, arguments, sessionId, currentProvider))
                    {
                        AnsiConsole.Write(content);
                    }
                    AnsiConsole.WriteLine();
                }
                else
                {
                    // 非流式输出
                    var result = await skillExecutor.ExecuteAsync(
                        skill, arguments, sessionId, currentProvider);

                    if (result.IsSuccess)
                    {
                        AnsiConsole.WriteLine(result.Value ?? string.Empty);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗ 执行失败: {result.Error}[/]");
                        Environment.Exit(1);
                    }
                }

                AnsiConsole.WriteLine();

                // 7. 清理临时会话（可选）
                if (isTemporarySession)
                {
                    AnsiConsole.MarkupLine("[dim]（已清理临时会话）[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 执行技能失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, skillNameArgument, sessionIdOption, providerOption, streamOption, argsArgument);

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
}
