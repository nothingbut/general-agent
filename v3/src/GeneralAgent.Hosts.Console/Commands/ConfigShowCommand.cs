using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

/// <summary>
/// config show 命令 - 显示配置
/// </summary>
public static class ConfigShowCommand
{
    /// <summary>
    /// 创建 config show 命令
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("show", "显示当前配置");

        // 选项：输出格式
        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            getDefaultValue: () => "table",
            description: "输出格式 (table, json)");
        command.AddOption(formatOption);

        command.SetHandler(async (format) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

                var result = await configService.GetConfigAsync();
                if (!result.IsSuccess)
                {
                    AnsiConsole.MarkupLine($"[red]✗ 读取配置失败: {result.Error}[/]");
                    Environment.Exit(1);
                    return;
                }

                var config = result.Value!;

                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    // JSON 格式输出
                    var json = System.Text.Json.JsonSerializer.Serialize(config,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.Console.WriteLine(json);
                }
                else
                {
                    // 表格格式输出
                    var table = new Table()
                        .Border(TableBorder.Rounded)
                        .AddColumn("配置项")
                        .AddColumn("当前值")
                        .AddColumn("说明");

                    table.AddRow("DefaultProvider", config.DefaultProvider, "默认 LLM 提供商");
                    table.AddRow("OllamaModel", config.OllamaModel, "Ollama 模型");
                    table.AddRow("OllamaBaseUrl", config.OllamaBaseUrl, "Ollama 端点");
                    table.AddRow("AnthropicApiKey",
                        string.IsNullOrEmpty(config.AnthropicApiKey) ? "[dim]未设置[/]" : "********",
                        "Anthropic API Key");
                    table.AddRow("AnthropicModel", config.AnthropicModel, "Anthropic 模型");
                    table.AddRow("DefaultSessionTitle", config.DefaultSessionTitle, "默认会话标题");
                    table.AddRow("EnableStreaming", config.EnableStreaming.ToString(), "启用流式输出");
                    table.AddRow("DefaultListLimit", config.DefaultListLimit.ToString(), "列表默认数量");

                    AnsiConsole.Write(table);

                    // 显示配置文件路径
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]配置文件: {configService.GetConfigFilePath()}[/]");

                    // 显示环境变量提示
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]环境变量:[/]");
                    AnsiConsole.MarkupLine("[dim]  AGENT_PROVIDER          - 覆盖默认提供商[/]");
                    AnsiConsole.MarkupLine("[dim]  AGENT_OLLAMA_MODEL      - 覆盖 Ollama 模型[/]");
                    AnsiConsole.MarkupLine("[dim]  AGENT_ANTHROPIC_API_KEY - 设置 Anthropic API Key[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 显示配置失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, formatOption);

        return command;
    }
}
