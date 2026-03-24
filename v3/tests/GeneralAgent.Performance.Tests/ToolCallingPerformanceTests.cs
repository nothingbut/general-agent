using System.Diagnostics;
using System.Text.Json.Nodes;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM.Serializers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Performance.Tests;

/// <summary>
/// Tool Calling 性能测试
/// 验证编排器开销和并行执行效率
/// </summary>
public class ToolCallingPerformanceTests
{
    /// <summary>
    /// 测试 1: Tool Calling 编排开销应小于 200ms
    /// 验证编排器本身的性能开销在合理范围内
    /// </summary>
    [Fact]
    public async Task ToolCallingOverhead_ShouldBeLessThan200ms()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Hello" }
        };

        // Warmup - 避免冷启动影响测量
        await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Act - 测量单次执行时间
        var stopwatch = Stopwatch.StartNew();
        await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"Tool Calling 开销 {stopwatch.ElapsedMilliseconds}ms 超过 200ms 限制");
    }

    /// <summary>
    /// 测试 2: 并行工具执行应接近理论时间
    /// 验证多个工具能够真正并行执行，而不是串行
    /// </summary>
    [Fact]
    public async Task ParallelToolExecution_ShouldBeFast()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var toolExecutor = provider.GetRequiredService<ToolExecutor>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册 3 个耗时 100ms 的 tool
        for (int i = 0; i < 3; i++)
        {
            var tool = CreateDelayTool($"tool{i}", TimeSpan.FromMilliseconds(100));
            registry.Register(tool);
        }

        var toolCalls = Enumerable.Range(0, 3)
            .Select(i => new ToolCall
            {
                Id = $"call_{i}",
                ToolName = $"tool{i}",
                Arguments = new Dictionary<string, object>()
            })
            .ToList();

        var context = new ToolExecutionContext { SessionId = Guid.NewGuid() };

        // Act - 并行执行 3 个工具
        var stopwatch = Stopwatch.StartNew();
        await toolExecutor.ExecuteManyAsync(toolCalls, context, null, CancellationToken.None);
        stopwatch.Stop();

        // Assert - 并行执行 3 个 100ms 的工具应该接近 100ms，而不是 300ms
        Assert.True(stopwatch.ElapsedMilliseconds < 150,
            $"并行执行耗时 {stopwatch.ElapsedMilliseconds}ms，未达到并行效果（预期 < 150ms）");
    }

    #region Helper Methods

    /// <summary>
    /// 配置测试所需的服务
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // 日志
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        // Tool Calling 配置
        var config = new ToolCallingConfig
        {
            Enabled = true,
            MaxRounds = 3,
            AbsoluteMaxRounds = 20,
            AutoExtendBy = 5
        };
        services.AddSingleton(Options.Create(config));

        // 核心服务
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<IToolSerializer, OpenAIToolSerializer>();
        services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();
        services.AddSingleton<ToolCallingOrchestrator>();

        // Mock LLM 客户端 - 直接返回响应，不调用工具
        var mockLLMClient = Substitute.For<ILLMClient>();
        mockLLMClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "测试响应",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        services.AddSingleton(mockLLMClient);
    }

    /// <summary>
    /// 创建一个模拟延迟的工具
    /// 用于测试并行执行性能
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <param name="delay">延迟时间</param>
    /// <returns>模拟工具实例</returns>
    private ITool CreateDelayTool(string name, TimeSpan delay)
    {
        var tool = Substitute.For<ITool>();
        tool.Name.Returns(name);
        tool.Description.Returns($"延迟 {delay.TotalMilliseconds}ms 的测试工具");
        tool.GetDefinition().Returns(new ToolDefinition
        {
            Name = name,
            Description = $"延迟 {delay.TotalMilliseconds}ms 的测试工具",
            InputSchema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject()
            }
        });
        tool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(delay);
                return Result<string>.Success("Done");
            });
        return tool;
    }

    #endregion
}
