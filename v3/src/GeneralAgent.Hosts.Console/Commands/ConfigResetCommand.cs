using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

public static class ConfigResetCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("reset", "重置配置到默认值");

        var forceOption = new Option<bool>("--force", "不询问直接重置");
        command.AddOption(forceOption);

        command.SetHandler(async (force) =>
        {
            try
            {
                if (!force && !AnsiConsole.Confirm("确认重置配置到默认值？"))
                {
                    AnsiConsole.MarkupLine("[yellow]已取消[/]");
                    return;
                }

                using var scope = serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

                var result = await configService.ResetConfigAsync();
                if (result.IsSuccess)
                {
                    AnsiConsole.MarkupLine("[green]✓ 配置已重置为默认值[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ 重置失败: {result.Error}[/]");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 重置配置失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, forceOption);

        return command;
    }
}
