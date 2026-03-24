using System.CommandLine;
using GeneralAgent.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GeneralAgent.Hosts.Console.Commands;

public static class ConfigSetCommand
{
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("set", "设置配置项");

        var keyArgument = new Argument<string>("key", "配置项名称");
        var valueArgument = new Argument<string>("value", "配置值");

        command.AddArgument(keyArgument);
        command.AddArgument(valueArgument);

        command.SetHandler(async (key, value) =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

                var result = await configService.UpdateConfigAsync(key, value);
                if (result.IsSuccess)
                {
                    AnsiConsole.MarkupLine($"[green]✓ 配置已更新: {key} = {value}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ 更新失败: {result.Error}[/]");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ 设置配置失败: {ex.Message}[/]");
                Environment.Exit(1);
            }
        }, keyArgument, valueArgument);

        return command;
    }
}
