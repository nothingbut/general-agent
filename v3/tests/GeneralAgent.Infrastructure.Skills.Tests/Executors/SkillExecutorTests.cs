using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.Skills.Tests.Executors;

public class SkillExecutorTests
{
    private readonly ILLMClientFactory _llmFactoryMock;
    private readonly ILLMClient _llmClientMock;
    private readonly IMessageRepository _messageRepoMock;
    private readonly ILogger<SkillExecutor> _loggerMock;
    private readonly SkillExecutor _executor;

    public SkillExecutorTests()
    {
        _llmFactoryMock = Substitute.For<ILLMClientFactory>();
        _llmClientMock = Substitute.For<ILLMClient>();
        _messageRepoMock = Substitute.For<IMessageRepository>();
        _loggerMock = Substitute.For<ILogger<SkillExecutor>>();

        // 默认返回模拟的 LLM 客户端
        _llmFactoryMock.GetClient(Arg.Any<string?>()).Returns(_llmClientMock);

        _executor = new SkillExecutor(_llmFactoryMock, _messageRepoMock, _loggerMock);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRenderTemplateAndCallLLM()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "你好 {{ user_name }}！今天有什么我可以帮助你的吗？",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "张三"
        };

        var expectedResponse = new CompletionResponse
        {
            Content = "你好！我可以帮你安排日程、回答问题或完成任务。",
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Is<CompletionRequest>(r =>
                r.Messages.Count == 1 &&
                r.Messages[0].Role == "user" &&
                r.Messages[0].Content.Contains("你好 张三！")),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResponse.Content);

        // 验证 LLM 被调用
        await _llmClientMock.Received(1).CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithContext_ShouldInjectMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "contextual",
            Description = "需要上下文的技能",
            Template = """
                基于以下对话历史：
                {{ for msg in context.messages }}
                {{ msg.role }}: {{ msg.content }}
                {{ end }}

                用户问题：{{ question }}
                """,
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "question",
                    Type = "string",
                    Required = true
                }
            },
            RequiresContext = true,
            ContextConfig = new ContextConfig
            {
                MaxMessages = 5,
                IncludeSystemMessages = false
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["question"] = "今天天气怎么样？"
        };

        // 模拟历史消息
        var historyMessages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.User, Content = "你好", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.Assistant, Content = "你好！有什么可以帮助你的吗？", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        _messageRepoMock.GetRecentAsync(sessionId, 5, Arg.Any<CancellationToken>())
            .Returns(historyMessages);

        var expectedResponse = new CompletionResponse
        {
            Content = "根据对话历史，今天是晴天。",
            Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 30 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResponse.Content);

        // 验证消息仓储被调用
        await _messageRepoMock.Received(1).GetRecentAsync(sessionId, 5, Arg.Any<CancellationToken>());

        // 验证渲染的模板包含上下文
        await _llmClientMock.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(r =>
                r.Messages[0].Content.Contains("你好") &&
                r.Messages[0].Content.Contains("你好！有什么可以帮助你的吗？") &&
                r.Messages[0].Content.Contains("今天天气怎么样？")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRoleFilter_ShouldFilterMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "user_only",
            Description = "只包含用户消息",
            Template = """
                用户历史消息：
                {{ for msg in context.messages }}
                - {{ msg.content }}
                {{ end }}
                """,
            Parameters = Array.Empty<SkillParameter>(),
            RequiresContext = true,
            ContextConfig = new ContextConfig
            {
                MaxMessages = 10,
                Roles = new[] { "user" },
                IncludeSystemMessages = false
            }
        };

        var arguments = new Dictionary<string, object>();

        // 模拟混合角色的历史消息
        var historyMessages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.User, Content = "用户消息1", CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.Assistant, Content = "助手消息1", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.User, Content = "用户消息2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        _messageRepoMock.GetRecentAsync(sessionId, 10, Arg.Any<CancellationToken>())
            .Returns(historyMessages);

        var expectedResponse = new CompletionResponse
        {
            Content = "已过滤用户消息。",
            Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 验证渲染的模板只包含用户消息
        await _llmClientMock.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(r =>
                r.Messages[0].Content.Contains("用户消息1") &&
                r.Messages[0].Content.Contains("用户消息2") &&
                !r.Messages[0].Content.Contains("助手消息1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExcludeSystem_ShouldNotIncludeSystemMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "no_system",
            Description = "不包含系统消息",
            Template = """
                对话历史：
                {{ for msg in context.messages }}
                {{ msg.role }}: {{ msg.content }}
                {{ end }}
                """,
            Parameters = Array.Empty<SkillParameter>(),
            RequiresContext = true,
            ContextConfig = new ContextConfig
            {
                MaxMessages = 10,
                IncludeSystemMessages = false
            }
        };

        var arguments = new Dictionary<string, object>();

        // 模拟包含系统消息的历史
        var historyMessages = new List<Message>
        {
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.System, Content = "系统消息", CreatedAt = DateTime.UtcNow.AddMinutes(-3) },
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.User, Content = "用户消息", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { Id = Guid.NewGuid(), SessionId = sessionId, Role = MessageRole.Assistant, Content = "助手消息", CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        _messageRepoMock.GetRecentAsync(sessionId, 10, Arg.Any<CancellationToken>())
            .Returns(historyMessages);

        var expectedResponse = new CompletionResponse
        {
            Content = "已排除系统消息。",
            Usage = new TokenUsage { PromptTokens = 30, CompletionTokens = 15 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 验证渲染的模板不包含系统消息
        await _llmClientMock.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(r =>
                !r.Messages[0].Content.Contains("系统消息") &&
                r.Messages[0].Content.Contains("用户消息") &&
                r.Messages[0].Content.Contains("助手消息")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParam_ShouldFail()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "你好 {{ user_name }}！",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>();

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("user_name");
        result.Error.Should().Contain("必填");

        // 验证 LLM 未被调用
        await _llmClientMock.DidNotReceive().CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultValue_ShouldApplyDefault()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候技能",
            Template = "{{ greeting }} {{ user_name }}！",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "greeting",
                    Type = "string",
                    Required = false,
                    DefaultValue = "你好"
                },
                new()
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["user_name"] = "李四"
        };

        var expectedResponse = new CompletionResponse
        {
            Content = "李四你好！",
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Is<CompletionRequest>(r => r.Messages[0].Content.Contains("你好 李四！")),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResponse.Content);

        // 验证模板使用了默认值
        await _llmClientMock.Received(1).CompleteAsync(
            Arg.Is<CompletionRequest>(r => r.Messages[0].Content.Contains("你好 李四！")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTemplate_ShouldFail()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "invalid",
            Description = "无效模板",
            Template = "{{ unclosed tag",
            Parameters = Array.Empty<SkillParameter>()
        };

        var arguments = new Dictionary<string, object>();

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("模板");

        // 验证 LLM 未被调用
        await _llmClientMock.DidNotReceive().CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldStreamFromLLM()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "streaming",
            Description = "流式技能",
            Template = "请回答：{{ question }}",
            Parameters = new List<SkillParameter>
            {
                new()
                {
                    Name = "question",
                    Type = "string",
                    Required = true
                }
            }
        };

        var arguments = new Dictionary<string, object>
        {
            ["question"] = "什么是AI？"
        };

        // 模拟流式响应
        var streamChunks = new[]
        {
            new StreamChunk { Delta = "AI ", IsComplete = false },
            new StreamChunk { Delta = "是 ", IsComplete = false },
            new StreamChunk { Delta = "人工智能。", IsComplete = true }
        };

        _llmClientMock.StreamAsync(
            Arg.Is<CompletionRequest>(r => r.Messages[0].Content.Contains("什么是AI？")),
            Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(streamChunks));

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in _executor.ExecuteStreamAsync(skill, arguments, sessionId))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().HaveCount(3);
        chunks[0].Should().Be("AI ");
        chunks[1].Should().Be("是 ");
        chunks[2].Should().Be("人工智能。");
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithProviderName_ShouldUseSpecifiedProvider()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var providerName = "CustomProvider";
        var skill = new Skill
        {
            Name = "test",
            Description = "测试技能",
            Template = "测试",
            Parameters = Array.Empty<SkillParameter>()
        };

        var arguments = new Dictionary<string, object>();

        var expectedResponse = new CompletionResponse
        {
            Content = "测试响应",
            Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 5 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId, providerName);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 验证使用了指定的提供商
        _llmFactoryMock.Received(1).GetClient(providerName);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutContext_ShouldNotFetchMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "simple",
            Description = "简单技能",
            Template = "简单提示词",
            Parameters = Array.Empty<SkillParameter>(),
            RequiresContext = false
        };

        var arguments = new Dictionary<string, object>();

        var expectedResponse = new CompletionResponse
        {
            Content = "简单响应",
            Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 5 },
            Timestamp = DateTime.UtcNow
        };

        _llmClientMock.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 验证消息仓储未被调用
        await _messageRepoMock.DidNotReceive().GetRecentAsync(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_LLMThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var skill = new Skill
        {
            Name = "test",
            Description = "测试技能",
            Template = "测试",
            Parameters = Array.Empty<SkillParameter>()
        };

        var arguments = new Dictionary<string, object>();

        _llmClientMock.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CompletionResponse>(new Exception("LLM 调用失败")));

        // Act
        var result = await _executor.ExecuteAsync(skill, arguments, sessionId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LLM 调用失败");
    }
}
