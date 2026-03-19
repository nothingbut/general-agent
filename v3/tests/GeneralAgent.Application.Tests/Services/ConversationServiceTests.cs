using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json.Nodes;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ConversationService 测试
/// 测试显式技能调用 (@skill, /skill) 和隐式工具调用两种模式
/// </summary>
public sealed class ConversationServiceTests
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILLMClientFactory _llmClientFactory;
    private readonly ToolRegistry _registry;
    private readonly ILLMClient _llmClient;
    private readonly IToolCallingListener _listener;
    private readonly IToolSerializer _serializer;
    private readonly ToolCallingOrchestrator _orchestrator;
    private readonly ToolExecutor _toolExecutor;
    private readonly ILogger<ConversationService> _logger;
    private readonly ConversationService _service;

    public ConversationServiceTests()
    {
        _sessionRepository = Substitute.For<ISessionRepository>();
        _messageRepository = Substitute.For<IMessageRepository>();
        _llmClientFactory = Substitute.For<ILLMClientFactory>();
        _logger = Substitute.For<ILogger<ConversationService>>();

        // 为 ToolExecutor 和 ToolCallingOrchestrator 创建真实实例
        var registryLogger = Substitute.For<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(registryLogger);
        _llmClient = Substitute.For<ILLMClient>();
        _listener = Substitute.For<IToolCallingListener>();
        _serializer = Substitute.For<IToolSerializer>();

        var config = Options.Create(new ToolCallingConfig
        {
            Enabled = true,
            MaxRounds = 5,
            AbsoluteMaxRounds = 10
        });

        var toolExecutorLogger = Substitute.For<ILogger<ToolExecutor>>();
        _toolExecutor = new ToolExecutor(_registry, toolExecutorLogger);

        var orchestratorLogger = Substitute.For<ILogger<ToolCallingOrchestrator>>();
        _orchestrator = new ToolCallingOrchestrator(
            _toolExecutor,
            _registry,
            _llmClient,
            _listener,
            _serializer,
            config,
            orchestratorLogger);

        _service = new ConversationService(
            _sessionRepository,
            _messageRepository,
            _llmClientFactory,
            _orchestrator,
            _toolExecutor,
            _logger);
    }

    [Fact]
    public async Task SendMessageAsync_WithAtSyntax_ShouldExecuteToolDirectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "@greeting user_name='Alice'";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // 注册一个模拟工具
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("greeting");
        mockTool.ExecuteAsync(
            Arg.Is<IReadOnlyDictionary<string, object>>(args => args["user_name"].ToString() == "Alice"),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("你好 Alice！今天有什么我可以帮助你的吗？"));

        _registry.Register(mockTool);

        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        Assert.Equal("你好 Alice！今天有什么我可以帮助你的吗？", response);

        // 验证保存了用户消息
        await _messageRepository.Received(1).CreateAsync(
            Arg.Is<Message>(m => m.Role == MessageRole.User && m.Content == userMessage),
            Arg.Any<CancellationToken>());

        // 验证保存了助手响应
        await _messageRepository.Received(1).CreateAsync(
            Arg.Is<Message>(m => m.Role == MessageRole.Assistant && m.Content == response),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_WithSlashSyntax_ShouldExecuteToolDirectly()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "/greeting user_name='Bob'";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // 注册一个模拟工具
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("greeting");
        mockTool.ExecuteAsync(
            Arg.Is<IReadOnlyDictionary<string, object>>(args => args.ContainsKey("user_name")),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("你好 Bob！今天有什么我可以帮助你的吗？"));

        _registry.Register(mockTool);

        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        Assert.Equal("你好 Bob！今天有什么我可以帮助你的吗？", response);
    }

    [Fact]
    public async Task SendMessageAsync_NoExplicitCall_ShouldDelegateToOrchestrator()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "Say hello to Charlie";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        _messageRepository.GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        // 模拟 LLM 响应（不调用工具）
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Hello Charlie! How can I help you today?",
                Model = "test-model",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null // 不调用工具
            });

        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        Assert.Equal("Hello Charlie! How can I help you today?", response);

        // 验证保存了用户消息和助手响应
        await _messageRepository.Received().CreateAsync(
            Arg.Any<Message>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_ToolFails_ShouldReturnErrorMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "@nonexistent arg='value'";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // 不注册工具，让它失败
        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        Assert.StartsWith("❌", response);
        Assert.Contains("工具未找到", response);

        // 验证错误消息被保存
        await _messageRepository.Received(1).CreateAsync(
            Arg.Is<Message>(m => m.Role == MessageRole.Assistant && m.Content.StartsWith("❌")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_ShouldSaveAllMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "Tell me a joke";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        _messageRepository.GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new List<Message>
            {
                Message.CreateUser(sessionId, "Previous message")
            });

        // 模拟 LLM 响应
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompletionResponse
            {
                Content = "Why did the chicken cross the road?",
                Model = "test-model",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow,
                ToolCalls = null
            });

        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        // 验证保存了 2 条消息：用户消息 + 最终响应
        await _messageRepository.Received(2).CreateAsync(
            Arg.Any<Message>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_WithNamespace_ShouldExecute()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "@personal:greeting user_name='David'";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // 注册一个带命名空间的工具
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("personal:greeting");
        mockTool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("你好 David！这是来自个人命名空间的问候。"));

        _registry.Register(mockTool);

        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        Assert.Equal("你好 David！这是来自个人命名空间的问候。", response);
    }

    [Fact]
    public async Task SendMessageAsync_SessionNotFound_ShouldThrow()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "Hello";

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns((Session?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.SendMessageAsync(sessionId, userMessage));
    }

    [Fact]
    public async Task SendMessageAsync_WithToolCalls_ShouldSaveToolMessages()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "What's the weather?";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        _messageRepository.GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        // 注册天气工具
        var weatherTool = Substitute.For<ITool>();
        weatherTool.Name.Returns("get_weather");
        weatherTool.GetDefinition().Returns(new ToolDefinition
        {
            Name = "get_weather",
            Description = "获取天气",
            InputSchema = new JsonObject()
        });
        weatherTool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<ToolExecutionContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("sunny, 25°C"));

        _registry.Register(weatherTool);

        // 模拟 LLM 第一次调用工具
        var firstResponse = new CompletionResponse
        {
            Content = "Let me check the weather.",
            Model = "test-model",
            Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 5 },
            Timestamp = DateTime.UtcNow,
            ToolCalls = new List<ToolCall>
            {
                new() { Id = "call_weather", ToolName = "get_weather", Arguments = new Dictionary<string, object>() }
            }
        };

        // 模拟 LLM 第二次返回最终结果
        var secondResponse = new CompletionResponse
        {
            Content = "The weather is sunny, 25°C.",
            Model = "test-model",
            Usage = new TokenUsage { PromptTokens = 15, CompletionTokens = 10 },
            Timestamp = DateTime.UtcNow,
            ToolCalls = null
        };

        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(firstResponse, secondResponse);

        _serializer.SerializeTools(Arg.Any<List<ToolDefinition>>())
            .Returns(new System.Text.Json.Nodes.JsonArray());

        // Act
        var response = await _service.SendMessageAsync(sessionId, userMessage);

        // Assert
        Assert.Equal("The weather is sunny, 25°C.", response);

        // 验证保存了多条消息
        await _messageRepository.Received().CreateAsync(
            Arg.Any<Message>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_WithProviderName_ShouldPassToContext()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userMessage = "@greeting user_name='Eve'";
        var providerName = "TestProvider";
        var session = Session.Create("测试会话");

        _sessionRepository.GetByIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(session);

        // 注册工具并捕获执行上下文
        var mockTool = Substitute.For<ITool>();
        mockTool.Name.Returns("greeting");
        ToolExecutionContext? capturedContext = null;
        mockTool.ExecuteAsync(
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Do<ToolExecutionContext>(ctx => capturedContext = ctx),
            Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("你好 Eve！"));

        _registry.Register(mockTool);

        // Act
        await _service.SendMessageAsync(sessionId, userMessage, providerName);

        // Assert
        Assert.NotNull(capturedContext);
        Assert.Equal(providerName, capturedContext?.ProviderName);
        Assert.Equal(sessionId, capturedContext?.SessionId);
    }
}
