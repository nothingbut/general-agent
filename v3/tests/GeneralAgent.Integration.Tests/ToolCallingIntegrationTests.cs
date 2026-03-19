using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.LLM.Serializers;
using GeneralAgent.Infrastructure.Skills;
using GeneralAgent.Infrastructure.Skills.Converters;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Integration.Tests;

/// <summary>
/// Phase 3.3 Tool Calling 集成测试
/// 验证 Listener、Serializer、Orchestrator 协同工作
/// </summary>
public class ToolCallingIntegrationTests
{
    /// <summary>
    /// 测试 1: 基础工具调用流程
    /// LLM 决定调用工具 → 工具执行成功 → LLM 返回最终响应
    /// </summary>
    [Fact]
    public async Task ToolCalling_WithSkill_ShouldExecuteAndReturnResponse()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        // 第一次调用: LLM 决定调用 greeting 工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Tools != null && req.Messages.Count == 1),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "Alice" } }
                }
            });

        // 第二次调用: LLM 生成最终响应（包含工具结果）
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Any(m => m.Role == "tool")),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "我已经成功向 Alice 打招呼了！",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var provider = CreateServiceProvider(mockClient);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册 greeting 技能作为工具
        var greetingSkill = CreateGreetingSkill();
        var skillTool = new SkillTool(
            greetingSkill,
            provider.GetRequiredService<ISkillExecutor>(),
            new SkillToToolConverter());
        registry.Register(skillTool);

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "请向 Alice 打招呼" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("我已经成功向 Alice 打招呼了！");
        result.TotalRounds.Should().Be(1);
        result.TotalToolCalls.Should().Be(1);
        result.Truncated.Should().BeFalse();

        // 验证消息历史包含所有消息
        result.Messages.Should().HaveCount(3); // user + assistant(tool_call) + tool(result)
        result.Messages[0].Role.Should().Be("user");
        result.Messages[1].Role.Should().Be("assistant");
        result.Messages[1].ToolCalls.Should().NotBeNull().And.HaveCount(1);
        result.Messages[2].Role.Should().Be("tool");
        result.Messages[2].Content.Should().Contain("Hello Alice");
    }

    /// <summary>
    /// 测试 2: 多轮工具调用
    /// LLM 连续调用多个工具 → 所有工具执行成功 → 对话历史正确维护
    /// </summary>
    [Fact]
    public async Task ToolCalling_MultipleToolCalls_ShouldExecuteInSequence()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        // 第一次调用: LLM 调用 greeting 工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 1),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "Bob" } }
                }
            });

        // 第二次调用: LLM 调用 calculator 工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 3),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_2", ToolName = "calculator", Arguments = new Dictionary<string, object> { ["a"] = 5, ["b"] = 3 } }
                }
            });

        // 第三次调用: LLM 生成最终响应
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 5),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "我向 Bob 打了招呼，并计算出结果是 8。",
                Usage = new TokenUsage { PromptTokens = 25, CompletionTokens = 15 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var provider = CreateServiceProvider(mockClient);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册多个工具
        var greetingSkill = CreateGreetingSkill();
        var calculatorSkill = CreateCalculatorSkill();
        var skillExecutor = provider.GetRequiredService<ISkillExecutor>();
        var converter = new SkillToToolConverter();

        registry.Register(new SkillTool(greetingSkill, skillExecutor, converter));
        registry.Register(new SkillTool(calculatorSkill, skillExecutor, converter));

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "向 Bob 打招呼并计算 5+3" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("我向 Bob 打了招呼，并计算出结果是 8。");
        result.TotalRounds.Should().Be(2);
        result.TotalToolCalls.Should().Be(2);
        result.Truncated.Should().BeFalse();

        // 验证消息历史
        result.Messages.Should().HaveCount(5);
        result.Messages[0].Role.Should().Be("user");
        result.Messages[1].Role.Should().Be("assistant");
        result.Messages[2].Role.Should().Be("tool");
        result.Messages[3].Role.Should().Be("assistant");
        result.Messages[4].Role.Should().Be("tool");
    }

    /// <summary>
    /// 测试 3: LLM 不调用工具
    /// LLM 直接返回响应 → 结果不包含工具调用
    /// </summary>
    [Fact]
    public async Task ToolCalling_NoToolCallsNeeded_ShouldReturnDirectResponse()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        // LLM 直接返回响应，不调用工具
        mockClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "你好！我是 AI 助手。",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 8 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var provider = CreateServiceProvider(mockClient);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册工具（但 LLM 不会使用）
        var greetingSkill = CreateGreetingSkill();
        registry.Register(new SkillTool(
            greetingSkill,
            provider.GetRequiredService<ISkillExecutor>(),
            new SkillToToolConverter()));

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "你是谁？" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("你好！我是 AI 助手。");
        result.TotalRounds.Should().Be(0);
        result.TotalToolCalls.Should().Be(0);
        result.Truncated.Should().BeFalse();

        // 验证只有原始用户消息
        result.Messages.Should().HaveCount(1);
        result.Messages[0].Role.Should().Be("user");
    }

    /// <summary>
    /// 测试 4: Tool Calling 禁用
    /// Config.Enabled = false → 直接调用 LLM → 不需要工具注册表
    /// </summary>
    [Fact]
    public async Task ToolCalling_Disabled_ShouldReturnDirectResponse()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        mockClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Tool Calling 已禁用，直接响应。",
                Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 10 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var config = new ToolCallingConfig
        {
            Enabled = false,
            MaxRounds = 3,
            AbsoluteMaxRounds = 20,
            AutoExtendBy = 5
        };

        var provider = CreateServiceProvider(mockClient, config);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "测试消息" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("Tool Calling 已禁用，直接响应。");
        result.TotalRounds.Should().Be(0);
        result.TotalToolCalls.Should().Be(0);
        result.Truncated.Should().BeFalse();

        // 验证 LLM 被调用时 Tools 参数为 null
        await mockClient.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Tools == null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 测试 5: 达到最大轮数限制
    /// LLM 持续调用工具 → 达到 MaxRounds → Listener 自动延长 → 最终完成或截断
    /// </summary>
    [Fact]
    public async Task ToolCalling_MaxRoundsReached_ShouldExtendOrStop()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        // 第 1 轮: LLM 调用 greeting 工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 1),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "User1" } }
                }
            });

        // 第 2 轮: LLM 调用 greeting 工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 3),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_2", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "User2" } }
                }
            });

        // 第 3 轮: LLM 调用 greeting 工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 5),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_3", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "User3" } }
                }
            });

        // 延长后的第 4 轮: LLM 返回最终响应
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 7),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "已完成所有打招呼。",
                Usage = new TokenUsage { PromptTokens = 30, CompletionTokens = 10 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var config = new ToolCallingConfig
        {
            Enabled = true,
            MaxRounds = 3,
            AbsoluteMaxRounds = 20,
            AutoExtendBy = 5 // AutomaticToolCallingListener 会自动延长
        };

        var provider = CreateServiceProvider(mockClient, config);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册工具
        var greetingSkill = CreateGreetingSkill();
        registry.Register(new SkillTool(
            greetingSkill,
            provider.GetRequiredService<ISkillExecutor>(),
            new SkillToToolConverter()));

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "向三个用户打招呼" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("已完成所有打招呼。");
        result.TotalRounds.Should().Be(3); // 3 轮工具调用
        result.TotalToolCalls.Should().Be(3);
        result.Truncated.Should().BeFalse();

        // 验证消息历史
        result.Messages.Should().HaveCount(7); // user + (assistant + tool) * 3
    }

    /// <summary>
    /// 测试 6: 批量工具调用
    /// LLM 在一轮中调用多个工具 → 所有工具并行执行 → 结果正确返回
    /// </summary>
    [Fact]
    public async Task ToolCalling_MultipleToolsInSingleRound_ShouldExecuteInParallel()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        // 第一次调用: LLM 同时调用多个工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 1),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 8 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "Alice" } },
                    new() { Id = "call_2", ToolName = "greeting", Arguments = new Dictionary<string, object> { ["user_name"] = "Bob" } },
                    new() { Id = "call_3", ToolName = "calculator", Arguments = new Dictionary<string, object> { ["a"] = 10, ["b"] = 20 } }
                }
            });

        // 第二次调用: LLM 生成最终响应
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 5), // user + assistant + 3 tool results
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "已完成所有操作。",
                Usage = new TokenUsage { PromptTokens = 30, CompletionTokens = 8 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var provider = CreateServiceProvider(mockClient);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册工具
        var greetingSkill = CreateGreetingSkill();
        var calculatorSkill = CreateCalculatorSkill();
        var skillExecutor = provider.GetRequiredService<ISkillExecutor>();
        var converter = new SkillToToolConverter();

        registry.Register(new SkillTool(greetingSkill, skillExecutor, converter));
        registry.Register(new SkillTool(calculatorSkill, skillExecutor, converter));

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "向 Alice 和 Bob 打招呼，并计算 10+20" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("已完成所有操作。");
        result.TotalRounds.Should().Be(1);
        result.TotalToolCalls.Should().Be(3);
        result.Truncated.Should().BeFalse();

        // 验证消息历史
        result.Messages.Should().HaveCount(5); // user + assistant + 3 tool results
        result.Messages.Count(m => m.Role == "tool").Should().Be(3);
    }

    /// <summary>
    /// 测试 7: 工具执行失败
    /// 工具执行失败 → 错误消息返回给 LLM → LLM 处理错误并返回响应
    /// </summary>
    [Fact]
    public async Task ToolCalling_ToolExecutionFails_ShouldHandleError()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        // 第一次调用: LLM 调用不存在的工具
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Count == 1),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", ToolName = "nonexistent_tool", Arguments = new Dictionary<string, object>() }
                }
            });

        // 第二次调用: LLM 处理错误并返回响应
        mockClient.CompleteAsync(
            Arg.Is<CompletionRequest>(req => req.Messages.Any(m => m.Role == "tool" && m.Content.Contains("错误"))),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "抱歉，该工具不可用。",
                Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 8 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var provider = CreateServiceProvider(mockClient);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "调用不存在的工具" }
        };

        // Act
        var result = await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert
        result.FinalResponse.Should().Be("抱歉，该工具不可用。");
        result.TotalRounds.Should().Be(1);
        result.TotalToolCalls.Should().Be(1);

        // 验证错误消息被传递给 LLM
        result.Messages[2].Role.Should().Be("tool");
        result.Messages[2].Content.Should().Contain("错误");
        result.Messages[2].Content.Should().Contain("nonexistent_tool");
    }

    /// <summary>
    /// 测试 8: 工具序列化
    /// 注册多个工具 → 序列化为 OpenAI 格式 → 传递给 LLM
    /// </summary>
    [Fact]
    public async Task ToolCalling_ToolSerialization_ShouldPassToolsToLLM()
    {
        // Arrange
        var mockClient = Substitute.For<ILLMClient>();

        mockClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "收到工具定义。",
                Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        var provider = CreateServiceProvider(mockClient);
        var orchestrator = provider.GetRequiredService<ToolCallingOrchestrator>();
        var registry = provider.GetRequiredService<ToolRegistry>();

        // 注册多个工具
        var greetingSkill = CreateGreetingSkill();
        var calculatorSkill = CreateCalculatorSkill();
        var skillExecutor = provider.GetRequiredService<ISkillExecutor>();
        var converter = new SkillToToolConverter();

        registry.Register(new SkillTool(greetingSkill, skillExecutor, converter));
        registry.Register(new SkillTool(calculatorSkill, skillExecutor, converter));

        var history = new List<ChatMessage>
        {
            new() { Role = "user", Content = "测试" }
        };

        // Act
        await orchestrator.ExecuteAsync(Guid.NewGuid(), history, null, CancellationToken.None);

        // Assert: 验证 LLM 收到了序列化的工具定义
        await mockClient.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(req =>
                req.Tools != null &&
                req.Tools.Count == 2),
            Arg.Any<CancellationToken>());
    }

    #region Helper Methods

    /// <summary>
    /// 创建服务提供器
    /// </summary>
    private ServiceProvider CreateServiceProvider(
        ILLMClient? mockClient = null,
        ToolCallingConfig? config = null)
    {
        var services = new ServiceCollection();

        // 日志
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // 配置
        var toolCallingConfig = config ?? new ToolCallingConfig
        {
            Enabled = true,
            MaxRounds = 3,
            AbsoluteMaxRounds = 20,
            AutoExtendBy = 5
        };
        services.AddSingleton(Options.Create(toolCallingConfig));

        // 核心服务
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<IToolSerializer, OpenAIToolSerializer>();
        services.AddSingleton<IToolCallingListener, AutomaticToolCallingListener>();
        services.AddSingleton<ToolCallingOrchestrator>();

        // 技能基础设施
        var effectiveClient = mockClient ?? CreateDefaultMockClient();

        // Mock LLM 客户端工厂（用于 SkillExecutor）
        var skillLLMClient = Substitute.For<ILLMClient>();
        skillLLMClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<CompletionRequest>();
                // 简单模拟：返回模板内容（对于技能执行）
                var content = req.Messages.LastOrDefault()?.Content ?? "Skill execution result";
                return Task.FromResult(new CompletionResponse
                {
                    Content = content,
                    Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 5 },
                    Timestamp = DateTime.UtcNow,
                    ToolCalls = null
                });
            });

        services.AddSingleton<ILLMClientFactory>(sp =>
        {
            var factory = Substitute.For<ILLMClientFactory>();
            factory.GetClient(Arg.Any<string>()).Returns(skillLLMClient);
            return factory;
        });
        services.AddSingleton<IMessageRepository>(sp => Substitute.For<IMessageRepository>());
        services.AddSingleton<ISkillExecutor, SkillExecutor>();

        // LLM 客户端（用于 Orchestrator）
        services.AddSingleton(effectiveClient);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建默认的 Mock LLM 客户端
    /// </summary>
    private ILLMClient CreateDefaultMockClient()
    {
        var client = Substitute.For<ILLMClient>();
        // 默认: 返回直接响应，不调用工具
        client.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "默认响应",
                Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 5 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });
        return client;
    }

    /// <summary>
    /// 创建 greeting 技能
    /// </summary>
    private Skill CreateGreetingSkill()
    {
        return new Skill
        {
            Name = "greeting",
            Namespace = null,
            Description = "向用户打招呼",
            Template = "Hello {{user_name}}! Nice to meet you.",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true,
                    Description = "用户的名字"
                }
            },
            RequiresContext = false,
            ReturnToLLM = true
        };
    }

    /// <summary>
    /// 创建 calculator 技能
    /// </summary>
    private Skill CreateCalculatorSkill()
    {
        return new Skill
        {
            Name = "calculator",
            Namespace = null,
            Description = "计算两个数字的和",
            Template = "{{a}} + {{b}} = {{sum}}",
            Parameters = new List<SkillParameter>
            {
                new() { Name = "a", Type = "int", Required = true, Description = "第一个数字" },
                new() { Name = "b", Type = "int", Required = true, Description = "第二个数字" }
            },
            RequiresContext = false,
            ReturnToLLM = true
        };
    }

    #endregion
}
