using FluentAssertions;
using GeneralAgent.Application;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure;
using GeneralAgent.Infrastructure.LLM;
using GeneralAgent.Infrastructure.LLM.Serializers;
using GeneralAgent.Infrastructure.Skills;
using GeneralAgent.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Integration.Tests;

/// <summary>
/// 技能系统端到端测试
/// 验证显式调用、隐式调用（Tool Calling）、上下文感知等完整功能
/// </summary>
public class SkillSystemE2ETests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly string _dbPath;
    private readonly string _tempSkillsDir;

    public SkillSystemE2ETests()
    {
        // 创建临时目录和数据库
        _dbPath = Path.Combine(Path.GetTempPath(), $"test_skills_e2e_{Guid.NewGuid()}.db");
        _tempSkillsDir = Path.Combine(Path.GetTempPath(), $"skills_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempSkillsDir);

        // 创建测试技能文件
        CreateTestSkillFiles();

        var services = new ServiceCollection();

        // 配置
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AgentDb"] = $"Data Source={_dbPath}",
                ["LLM:DefaultProvider"] = "Mock",
                ["LLM:Providers:Mock:Name"] = "Mock",
                ["LLM:Providers:Mock:BaseUrl"] = "http://localhost:11434",
                ["LLM:Providers:Mock:DefaultModel"] = "mock-model",
                ["LLM:Providers:Mock:TimeoutSeconds"] = "120"
            })
            .Build();

        // 注册所有层
        services.AddInfrastructure($"Data Source={_dbPath}");
        // 不注册真实的 LLM，我们会手动注册 Mock
        // services.AddLLMInfrastructure(configuration);
        services.AddApplicationLayer(configuration);

        // 注册 Mock LLM 客户端（用于技能执行）
        var mockLLMClient = Substitute.For<ILLMClient>();
        mockLLMClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<CompletionRequest>();
                // 模拟技能执行：返回渲染后的提示词（技能执行器已经渲染过模板了）
                // 对于显式技能调用，技能执行器会先渲染模板，然后发送给 LLM
                // 所以 Mock 直接返回收到的内容即可（模拟 LLM 确认执行）
                var content = req.Messages.LastOrDefault()?.Content ?? "Skill execution result";
                return Task.FromResult(new CompletionResponse
                {
                    Content = content,
                    Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 5 },
                    Timestamp = DateTime.UtcNow,
                    ToolCalls = null
                });
            });

        // 注册 Mock ILLMClientFactory
        var mockFactory = Substitute.For<ILLMClientFactory>();
        mockFactory.GetClient(Arg.Any<string?>()).Returns(mockLLMClient);
        services.AddSingleton(mockFactory);

        // 注册日志
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        _serviceProvider = services.BuildServiceProvider();

        // 确保数据库已创建
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AgentDbContext>();
        context.Database.EnsureCreated();

        // 加载测试技能
        var skillService = scope.ServiceProvider.GetRequiredService<SkillService>();
        var loadResult = skillService.LoadSkillsAsync(_tempSkillsDir).GetAwaiter().GetResult();
        if (!loadResult.IsSuccess)
        {
            throw new InvalidOperationException($"加载测试技能失败: {loadResult.Error}");
        }
    }

    /// <summary>
    /// 测试 1: 显式技能调用 (@skill 语法)
    /// 用户直接调用 @greeting 技能，应该执行技能并返回结果
    /// </summary>
    [Fact]
    public async Task ExplicitSkillCall_WithAtSyntax_ShouldReturnSkillResponse()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var session = await sessionService.CreateSessionAsync("显式调用测试");

        // Act - 使用 @ 语法调用 greeting 技能
        var response = await conversationService.SendMessageAsync(
            session.Id,
            "@greeting user_name='Charlie'");

        // Assert
        response.Should().NotBeNullOrEmpty();
        response.Should().Contain("Charlie"); // 技能应该使用参数
        response.Should().Contain("Hello"); // greeting 技能的响应

        // 验证消息持久化
        var messages = await messageRepo.GetBySessionAsync(session.Id);
        messages.Should().HaveCount(2); // user + assistant
        messages[0].Content.Should().Be("@greeting user_name='Charlie'");
        messages[1].Content.Should().Be(response);
    }

    /// <summary>
    /// 测试 2: 显式技能调用 (/skill 语法)
    /// 用户使用 / 命令风格调用技能，应该同样工作
    /// </summary>
    [Fact]
    public async Task ExplicitSkillCall_WithSlashSyntax_ShouldReturnSkillResponse()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();

        var session = await sessionService.CreateSessionAsync("命令风格测试");

        // Act - 使用 / 语法调用 greeting 技能
        var response = await conversationService.SendMessageAsync(
            session.Id,
            "/greeting user_name='David'");

        // Assert
        response.Should().NotBeNullOrEmpty();
        response.Should().Contain("David");
        response.Should().Contain("Hello");
    }

    /// <summary>
    /// 测试 3: 隐式技能调用（Tool Calling）
    /// 验证技能被正确注册为工具，可以被 Tool Calling 系统发现
    /// 注意：此测试验证架构集成，不测试完整的 LLM Tool Calling 流程
    /// （完整流程在 ToolCallingIntegrationTests 中测试）
    /// </summary>
    [Fact]
    public async Task ImplicitSkillCall_SkillsAreRegisteredAsTools_CanBeDiscovered()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var toolRegistry = scope.ServiceProvider.GetRequiredService<ToolRegistry>();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();

        // Act - 获取所有注册的工具
        var allTools = toolRegistry.GetAllTools();
        var greetingTool = toolRegistry.GetTool("greeting");

        // Assert - 验证技能被注册为工具
        allTools.Should().Contain(t => t.Name == "greeting");
        allTools.Should().Contain(t => t.Name == "summarize");

        greetingTool.Should().NotBeNull();
        greetingTool!.Description.Should().Be("向用户打招呼");

        // 验证工具可以被 Tool Calling 系统发现和序列化
        var serializer = scope.ServiceProvider.GetRequiredService<IToolSerializer>();
        var toolDefinitions = allTools.Select(t => t.GetDefinition()).ToList();
        var serializedTools = serializer.SerializeTools(toolDefinitions);

        serializedTools.Should().NotBeNull();
        serializedTools.Count.Should().BeGreaterOrEqualTo(2);
    }

    /// <summary>
    /// 测试 3.5: 隐式技能调用 - LLM 选择并执行技能
    /// 用户发送自然语言 → LLM 识别需要调用技能 → 返回 ToolCall → 技能执行成功 → 返回包含结果的响应
    /// </summary>
    [Fact]
    public async Task ImplicitSkillCall_LLMSelectsSkill_ShouldExecuteCorrectSkill()
    {
        // Arrange - 创建专门的 Mock LLM Client 用于 Tool Calling
        var mockOrchestratorClient = Substitute.For<ILLMClient>();
        var mockSkillExecutorClient = Substitute.For<ILLMClient>();

        // 配置 Orchestrator 的 LLM: 第一次返回 ToolCall，第二次返回最终响应
        int callCount = 0;
        mockOrchestratorClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                var req = ci.Arg<CompletionRequest>();

                // 第一次调用: LLM 识别自然语言并返回 ToolCall
                if (callCount == 1)
                {
                    return Task.FromResult(new CompletionResponse
                    {
                        Content = "",
                        Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
                        Timestamp = DateTime.UtcNow,
                        ToolCalls = new List<ToolCall>
                        {
                            new()
                            {
                                Id = "call_implicit_1",
                                ToolName = "greeting",
                                Arguments = new Dictionary<string, object> { ["user_name"] = "David" }
                            }
                        }
                    });
                }

                // 第二次调用: LLM 生成包含工具结果的最终响应
                return Task.FromResult(new CompletionResponse
                {
                    Content = "我已经成功向 David 打招呼了！",
                    Usage = new TokenUsage { PromptTokens = 20, CompletionTokens = 10 },
                    Timestamp = DateTime.UtcNow,
                    ToolCalls = null
                });
            });

        // 配置 SkillExecutor 的 LLM: 渲染技能模板
        mockSkillExecutorClient.CompleteAsync(
            Arg.Any<CompletionRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var req = ci.Arg<CompletionRequest>();
                var content = req.Messages.LastOrDefault()?.Content ?? "Skill execution result";
                return Task.FromResult(new CompletionResponse
                {
                    Content = content,
                    Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 5 },
                    Timestamp = DateTime.UtcNow,
                    ToolCalls = null
                });
            });

        // 创建新的服务提供器，使用自定义的 Mock
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_implicit_skill_{Guid.NewGuid()}.db");
        var testSkillsDir = Path.Combine(Path.GetTempPath(), $"skills_implicit_{Guid.NewGuid()}");
        Directory.CreateDirectory(testSkillsDir);

        try
        {
            // 创建测试技能文件
            var greetingContent = @"---
name: greeting
description: 向用户打招呼
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

Hello {{user_name}}! Nice to meet you!";
            File.WriteAllText(Path.Combine(testSkillsDir, "greeting.md"), greetingContent);

            var services = new ServiceCollection();

            // 配置
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AgentDb"] = $"Data Source={testDbPath}",
                    ["LLM:DefaultProvider"] = "Mock",
                    ["LLM:Providers:Mock:Name"] = "Mock",
                    ["LLM:Providers:Mock:BaseUrl"] = "http://localhost:11434",
                    ["LLM:Providers:Mock:DefaultModel"] = "mock-model",
                    ["LLM:Providers:Mock:TimeoutSeconds"] = "120"
                })
                .Build();

            // 注册基础设施层
            services.AddInfrastructure($"Data Source={testDbPath}");
            services.AddApplicationLayer(configuration);

            // 注册 Mock LLM 客户端工厂
            var mockFactory = Substitute.For<ILLMClientFactory>();
            mockFactory.GetClient(Arg.Any<string?>()).Returns(mockOrchestratorClient);
            services.AddSingleton(mockFactory);

            // 替换默认的 LLM 客户端为 orchestrator 的 mock
            services.AddSingleton(mockOrchestratorClient);

            // 注册日志
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            using var provider = services.BuildServiceProvider();

            // 初始化数据库
            using var initScope = provider.CreateScope();
            var context = initScope.ServiceProvider.GetRequiredService<AgentDbContext>();
            context.Database.EnsureCreated();

            // 加载测试技能
            var skillService = initScope.ServiceProvider.GetRequiredService<SkillService>();
            var loadResult = await skillService.LoadSkillsAsync(testSkillsDir);
            if (!loadResult.IsSuccess)
            {
                throw new InvalidOperationException($"加载测试技能失败: {loadResult.Error}");
            }

            // Act
            using var testScope = provider.CreateScope();
            var sessionService = testScope.ServiceProvider.GetRequiredService<SessionService>();
            var conversationService = testScope.ServiceProvider.GetRequiredService<ConversationService>();

            var session = await sessionService.CreateSessionAsync("隐式技能调用测试");
            var response = await conversationService.SendMessageAsync(
                session.Id,
                "帮我向 David 打个招呼");

            // Assert
            response.Should().NotBeNullOrEmpty();
            response.Should().Contain("David"); // 响应应该包含用户名
            response.Should().Contain("成功"); // 最终响应应该包含 LLM 返回的内容

            // 验证 LLM 被调用了多次（包括工具调用和最终响应生成）
            await mockOrchestratorClient.Received().CompleteAsync(
                Arg.Any<CompletionRequest>(),
                Arg.Any<CancellationToken>());

            // 验证有调用带有工具定义（说明 Tool Calling 流程被触发）
            await mockOrchestratorClient.Received().CompleteAsync(
                Arg.Is<CompletionRequest>(req =>
                    req.Tools != null &&
                    req.Tools.Count > 0),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            // 清理
            if (File.Exists(testDbPath))
            {
                try { File.Delete(testDbPath); } catch { /* ignore */ }
            }
            if (Directory.Exists(testSkillsDir))
            {
                try { Directory.Delete(testSkillsDir, recursive: true); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>
    /// 测试 4: 上下文感知技能
    /// 技能应该能够访问会话历史，提供上下文相关的响应
    /// </summary>
    [Fact]
    public async Task ContextAwareSkill_ShouldAccessConversationHistory()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();
        var messageRepo = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

        var session = await sessionService.CreateSessionAsync("上下文测试");

        // 建立上下文：发送第一条消息
        await conversationService.SendMessageAsync(
            session.Id,
            "我叫 Frank，我喜欢编程");

        // Act - 调用 summarize 技能，应该能够总结之前的对话
        var response = await conversationService.SendMessageAsync(
            session.Id,
            "@summarize count=1");

        // Assert
        response.Should().NotBeNullOrEmpty();
        // summarize 技能应该包含之前对话的内容
        // 注意：这里的断言取决于 summarize 技能的实际实现

        // 验证消息历史
        var messages = await messageRepo.GetBySessionAsync(session.Id);
        messages.Should().HaveCount(4); // 2 user messages + 2 assistant responses
        messages[0].Content.Should().Contain("Frank");
        messages[2].Content.Should().Contain("summarize");
    }

    /// <summary>
    /// 测试 5: 技能调用错误处理
    /// 当技能调用失败时，应该返回友好的错误消息
    /// </summary>
    [Fact]
    public async Task SkillCall_WithInvalidParameters_ShouldReturnErrorMessage()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();

        var session = await sessionService.CreateSessionAsync("错误处理测试");

        // Act - 调用技能但缺少必需参数
        var response = await conversationService.SendMessageAsync(
            session.Id,
            "@greeting"); // 缺少 user_name 参数

        // Assert
        response.Should().NotBeNullOrEmpty();
        response.Should().Contain("❌"); // 应该有错误标记
        // 或者根据实际错误消息格式调整断言
    }

    /// <summary>
    /// 测试 6: 流式模式下的显式技能调用
    /// 流式模式应该正确处理技能调用
    /// </summary>
    [Fact]
    public async Task ExplicitSkillCall_InStreamingMode_ShouldReturnChunks()
    {
        // Arrange
        using var scope = _serviceProvider.CreateScope();
        var sessionService = scope.ServiceProvider.GetRequiredService<SessionService>();
        var conversationService = scope.ServiceProvider.GetRequiredService<ConversationService>();

        var session = await sessionService.CreateSessionAsync("流式技能测试");

        // Act - 流式调用技能
        var chunks = new List<string>();
        await foreach (var chunk in conversationService.SendMessageStreamAsync(
            session.Id,
            "@greeting user_name='Grace'"))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().NotBeEmpty();
        var fullResponse = string.Join("", chunks);
        fullResponse.Should().Contain("Grace");
        fullResponse.Should().Contain("Hello");
    }

    /// <summary>
    /// 创建测试技能文件
    /// </summary>
    private void CreateTestSkillFiles()
    {
        // greeting 技能
        var greetingContent = @"---
name: greeting
description: 向用户打招呼
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

Hello {{user_name}}! Nice to meet you!";

        File.WriteAllText(
            Path.Combine(_tempSkillsDir, "greeting.md"),
            greetingContent);

        // summarize 技能
        var summarizeContent = @"---
name: summarize
description: 总结最近的对话内容
parameters:
  - name: count
    type: integer
    required: false
    description: 要总结的消息数量（默认为 5）
---

请总结最近 {{count}} 条对话的主要内容。保持简洁，突出重点。";

        File.WriteAllText(
            Path.Combine(_tempSkillsDir, "summarize.md"),
            summarizeContent);
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();

        // 清理临时数据库
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // 忽略清理失败
            }
        }

        // 清理临时技能目录
        if (Directory.Exists(_tempSkillsDir))
        {
            try
            {
                Directory.Delete(_tempSkillsDir, recursive: true);
            }
            catch
            {
                // 忽略清理失败
            }
        }
    }
}
