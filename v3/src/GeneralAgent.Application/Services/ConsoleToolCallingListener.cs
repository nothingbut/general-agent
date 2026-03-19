using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 控制台交互式 Tool Calling 监听器
/// 在达到最大轮数时通过控制台与用户交互，决定是否继续
/// </summary>
public sealed class ConsoleToolCallingListener : IToolCallingListener
{
    /// <inheritdoc />
    public Task<ExtendDecision> OnMaxRoundsReachedAsync(
        int currentRounds,
        Guid sessionId,
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.WriteLine($"⚠️  Tool Calling 已执行 {currentRounds} 轮");
        Console.WriteLine($"   会话 ID: {sessionId}");
        Console.WriteLine($"   工具调用数量: {toolCalls.Count}");
        Console.WriteLine();
        Console.WriteLine("是否继续？");
        Console.WriteLine("  [y] 继续 3 轮");
        Console.WriteLine("  [5] 继续 5 轮");
        Console.WriteLine("  [10] 继续 10 轮");
        Console.WriteLine("  [n] 停止");
        Console.Write("> ");

        var input = Console.ReadLine()?.Trim().ToLowerInvariant();

        var decision = input switch
        {
            "y" or "yes" => new ExtendDecision { Stop = false, ExtendBy = 3 },
            "5" => new ExtendDecision { Stop = false, ExtendBy = 5 },
            "10" => new ExtendDecision { Stop = false, ExtendBy = 10 },
            "n" or "no" => new ExtendDecision { Stop = true, ExtendBy = 0 },
            _ => new ExtendDecision { Stop = false, ExtendBy = 3 } // 默认继续 3 轮
        };

        return Task.FromResult(decision);
    }
}
