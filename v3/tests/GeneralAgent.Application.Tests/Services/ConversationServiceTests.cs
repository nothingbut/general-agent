using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using NSubstitute;
using Xunit;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ConversationService 测试
/// </summary>
public sealed class ConversationServiceTests
{
    private readonly ISessionRepository _mockSessionRepo;
    private readonly IMessageRepository _mockMessageRepo;
    private readonly ILLMClientFactory _mockClientFactory;
    private readonly ILLMClient _mockLLMClient;
    private readonly ConversationService _service;
    private readonly Guid _testSessionId;

    public ConversationServiceTests()
    {
        _mockSessionRepo = Substitute.For<ISessionRepository>();
        _mockMessageRepo = Substitute.For<IMessageRepository>();
        _mockClientFactory = Substitute.For<ILLMClientFactory>();
        _mockLLMClient = Substitute.For<ILLMClient>();
        _testSessionId = Guid.NewGuid();

        // 默认配置：工厂返回模拟客户端
        _mockClientFactory
            .GetClient(Arg.Any<string?>())
            .Returns(_mockLLMClient);

        _service = new ConversationService(
            _mockSessionRepo,
            _mockMessageRepo,
            _mockClientFactory);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSessionRepositoryIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConversationService(null!, _mockMessageRepo, _mockClientFactory));
        Assert.Equal("sessionRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenMessageRepositoryIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConversationService(_mockSessionRepo, null!, _mockClientFactory));
        Assert.Equal("messageRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLLMClientFactoryIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConversationService(_mockSessionRepo, _mockMessageRepo, null!));
        Assert.Equal("llmClientFactory", ex.ParamName);
    }

    [Fact]
    public async Task SendMessageAsync_FirstMessage_InjectsSystemPrompt()
    {
        // Arrange
        var userMessage = "你好";
        var expectedResponse = "你好！有什么可以帮助你的吗？";

        _mockSessionRepo
            .GetByIdAsync(_testSessionId, default)
            .Returns(Session.Create("测试会话") with { Id = _testSessionId });

        _mockMessageRepo
            .GetBySessionAsync(_testSessionId, default)
            .Returns(new List<Message>());

        _mockMessageRepo
            .CreateAsync(Arg.Any<Message>(), default)
            .Returns(call => call.Arg<Message>());

        _mockLLMClient
            .CompleteAsync(Arg.Any<CompletionRequest>(), default)
            .Returns(new CompletionResponse
            {
                Content = expectedResponse,
                Model = "test-model",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var response = await _service.SendMessageAsync(_testSessionId, userMessage);

        // Assert
        Assert.Equal(expectedResponse, response);
        await _mockMessageRepo.Received(1).CreateAsync(
            Arg.Is<Message>(m => m.Role == MessageRole.User && m.Content == userMessage),
            default);
        await _mockMessageRepo.Received(1).CreateAsync(
            Arg.Is<Message>(m => m.Role == MessageRole.Assistant && m.Content == expectedResponse),
            default);
    }

    [Fact]
    public async Task SendMessageAsync_WithHistory_UsesHistoryMessages()
    {
        // Arrange
        var userMessage = "第二条消息";
        var expectedResponse = "收到第二条消息";

        _mockSessionRepo
            .GetByIdAsync(_testSessionId, default)
            .Returns(Session.Create("测试会话") with { Id = _testSessionId });

        var existingMessages = new List<Message>
        {
            Message.CreateUser(_testSessionId, "第一条用户消息"),
            Message.CreateAssistant(_testSessionId, "第一条助手响应")
        };
        _mockMessageRepo
            .GetBySessionAsync(_testSessionId, default)
            .Returns(existingMessages);

        _mockMessageRepo
            .CreateAsync(Arg.Any<Message>(), default)
            .Returns(call => call.Arg<Message>());

        _mockLLMClient
            .CompleteAsync(Arg.Any<CompletionRequest>(), default)
            .Returns(new CompletionResponse
            {
                Content = expectedResponse,
                Model = "test-model",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        var response = await _service.SendMessageAsync(_testSessionId, userMessage);

        // Assert
        Assert.Equal(expectedResponse, response);
    }

    [Fact]
    public async Task SendMessageAsync_ThrowsInvalidOperationException_WhenSessionNotFound()
    {
        // Arrange
        _mockSessionRepo
            .GetByIdAsync(_testSessionId, default)
            .Returns((Session?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SendMessageAsync(_testSessionId, "测试"));
        Assert.Contains("会话不存在", ex.Message);
    }

    [Fact]
    public async Task SendMessageAsync_WithProviderName_UsesSpecifiedProvider()
    {
        // Arrange
        var providerName = "TestProvider";

        _mockSessionRepo
            .GetByIdAsync(_testSessionId, default)
            .Returns(Session.Create("测试会话") with { Id = _testSessionId });

        _mockMessageRepo
            .GetBySessionAsync(_testSessionId, default)
            .Returns(new List<Message>());

        _mockMessageRepo
            .CreateAsync(Arg.Any<Message>(), default)
            .Returns(call => call.Arg<Message>());

        _mockLLMClient
            .CompleteAsync(Arg.Any<CompletionRequest>(), default)
            .Returns(new CompletionResponse
            {
                Content = "响应",
                Model = "test-model",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        await _service.SendMessageAsync(_testSessionId, "测试", providerName);

        // Assert
        _mockClientFactory.Received(1).GetClient(providerName);
    }

    [Fact]
    public async Task SendMessageStreamAsync_StreamsResponse_AndSavesCompleteMessage()
    {
        // Arrange
        var userMessage = "流式测试";
        var chunks = new[] { "你", "好", "！" };
        var expectedFullResponse = string.Join("", chunks);

        _mockSessionRepo
            .GetByIdAsync(_testSessionId, default)
            .Returns(Session.Create("测试会话") with { Id = _testSessionId });

        _mockMessageRepo
            .GetBySessionAsync(_testSessionId, default)
            .Returns(new List<Message>());

        _mockMessageRepo
            .CreateAsync(Arg.Any<Message>(), default)
            .Returns(call => call.Arg<Message>());

        _mockLLMClient
            .StreamAsync(Arg.Any<CompletionRequest>(), default)
            .Returns(CreateAsyncEnumerable(chunks));

        // Act
        var receivedChunks = new List<string>();
        await foreach (var chunk in _service.SendMessageStreamAsync(_testSessionId, userMessage))
        {
            receivedChunks.Add(chunk);
        }

        // Assert
        Assert.Equal(chunks, receivedChunks);
        await _mockMessageRepo.Received(1).CreateAsync(
            Arg.Is<Message>(m =>
                m.Role == MessageRole.Assistant &&
                m.Content == expectedFullResponse),
            default);
    }

    [Fact]
    public async Task SendMessageStreamAsync_ThrowsInvalidOperationException_WhenSessionNotFound()
    {
        // Arrange
        _mockSessionRepo
            .GetByIdAsync(_testSessionId, default)
            .Returns((Session?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in _service.SendMessageStreamAsync(_testSessionId, "测试"))
            {
            }
        });
    }

    private static async IAsyncEnumerable<StreamChunk> CreateAsyncEnumerable(string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return new StreamChunk
            {
                Delta = chunk,
                IsComplete = false
            };
        }
    }
}
