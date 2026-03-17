using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Moq;
using Xunit;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// ConversationService 测试
/// </summary>
public sealed class ConversationServiceTests
{
    private readonly Mock<ISessionRepository> _mockSessionRepo;
    private readonly Mock<IMessageRepository> _mockMessageRepo;
    private readonly Mock<ILLMClientFactory> _mockClientFactory;
    private readonly Mock<ILLMClient> _mockLLMClient;
    private readonly ConversationService _service;
    private readonly Guid _testSessionId;

    public ConversationServiceTests()
    {
        _mockSessionRepo = new Mock<ISessionRepository>();
        _mockMessageRepo = new Mock<IMessageRepository>();
        _mockClientFactory = new Mock<ILLMClientFactory>();
        _mockLLMClient = new Mock<ILLMClient>();
        _testSessionId = Guid.NewGuid();

        // 默认配置：工厂返回模拟客户端
        _mockClientFactory
            .Setup(f => f.GetClient(It.IsAny<string?>()))
            .Returns(_mockLLMClient.Object);

        _service = new ConversationService(
            _mockSessionRepo.Object,
            _mockMessageRepo.Object,
            _mockClientFactory.Object);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenSessionRepositoryIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConversationService(null!, _mockMessageRepo.Object, _mockClientFactory.Object));
        Assert.Equal("sessionRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenMessageRepositoryIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConversationService(_mockSessionRepo.Object, null!, _mockClientFactory.Object));
        Assert.Equal("messageRepository", ex.ParamName);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenLLMClientFactoryIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ConversationService(_mockSessionRepo.Object, _mockMessageRepo.Object, null!));
        Assert.Equal("llmClientFactory", ex.ParamName);
    }

    [Fact]
    public async Task SendMessageAsync_FirstMessage_InjectsSystemPrompt()
    {
        // Arrange
        var userMessage = "你好";
        var expectedResponse = "你好！有什么可以帮助你的吗？";

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(_testSessionId, default))
            .ReturnsAsync(Session.Create("测试会话") with { Id = _testSessionId });

        _mockMessageRepo
            .Setup(r => r.GetBySessionAsync(_testSessionId, default))
            .ReturnsAsync(new List<Message>());

        _mockMessageRepo
            .Setup(r => r.CreateAsync(It.IsAny<Message>(), default))
            .ReturnsAsync((Message m, CancellationToken ct) => m);

        _mockLLMClient
            .Setup(c => c.CompleteAsync(It.IsAny<CompletionRequest>(), default))
            .ReturnsAsync(new CompletionResponse
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
        _mockMessageRepo.Verify(r => r.CreateAsync(
            It.Is<Message>(m => m.Role == MessageRole.User && m.Content == userMessage),
            default), Times.Once);
        _mockMessageRepo.Verify(r => r.CreateAsync(
            It.Is<Message>(m => m.Role == MessageRole.Assistant && m.Content == expectedResponse),
            default), Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithHistory_UsesHistoryMessages()
    {
        // Arrange
        var userMessage = "第二条消息";
        var expectedResponse = "收到第二条消息";

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(_testSessionId, default))
            .ReturnsAsync(Session.Create("测试会话") with { Id = _testSessionId });

        var existingMessages = new List<Message>
        {
            Message.CreateUser(_testSessionId, "第一条用户消息"),
            Message.CreateAssistant(_testSessionId, "第一条助手响应")
        };
        _mockMessageRepo
            .Setup(r => r.GetBySessionAsync(_testSessionId, default))
            .ReturnsAsync(existingMessages);

        _mockMessageRepo
            .Setup(r => r.CreateAsync(It.IsAny<Message>(), default))
            .ReturnsAsync((Message m, CancellationToken ct) => m);

        _mockLLMClient
            .Setup(c => c.CompleteAsync(It.IsAny<CompletionRequest>(), default))
            .ReturnsAsync(new CompletionResponse
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
            .Setup(r => r.GetByIdAsync(_testSessionId, default))
            .ReturnsAsync((Session?)null);

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
            .Setup(r => r.GetByIdAsync(_testSessionId, default))
            .ReturnsAsync(Session.Create("测试会话") with { Id = _testSessionId });

        _mockMessageRepo
            .Setup(r => r.GetBySessionAsync(_testSessionId, default))
            .ReturnsAsync(new List<Message>());

        _mockMessageRepo
            .Setup(r => r.CreateAsync(It.IsAny<Message>(), default))
            .ReturnsAsync((Message m, CancellationToken ct) => m);

        _mockLLMClient
            .Setup(c => c.CompleteAsync(It.IsAny<CompletionRequest>(), default))
            .ReturnsAsync(new CompletionResponse
            {
                Content = "响应",
                Model = "test-model",
                Usage = new TokenUsage { PromptTokens = 10, CompletionTokens = 20 },
                Timestamp = DateTime.UtcNow
            });

        // Act
        await _service.SendMessageAsync(_testSessionId, "测试", providerName);

        // Assert
        _mockClientFactory.Verify(f => f.GetClient(providerName), Times.Once);
    }

    [Fact]
    public async Task SendMessageStreamAsync_StreamsResponse_AndSavesCompleteMessage()
    {
        // Arrange
        var userMessage = "流式测试";
        var chunks = new[] { "你", "好", "！" };
        var expectedFullResponse = string.Join("", chunks);

        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(_testSessionId, default))
            .ReturnsAsync(Session.Create("测试会话") with { Id = _testSessionId });

        _mockMessageRepo
            .Setup(r => r.GetBySessionAsync(_testSessionId, default))
            .ReturnsAsync(new List<Message>());

        _mockMessageRepo
            .Setup(r => r.CreateAsync(It.IsAny<Message>(), default))
            .ReturnsAsync((Message m, CancellationToken ct) => m);

        _mockLLMClient
            .Setup(c => c.StreamAsync(It.IsAny<CompletionRequest>(), default))
            .Returns(CreateAsyncEnumerable(chunks));

        // Act
        var receivedChunks = new List<string>();
        await foreach (var chunk in _service.SendMessageStreamAsync(_testSessionId, userMessage))
        {
            receivedChunks.Add(chunk);
        }

        // Assert
        Assert.Equal(chunks, receivedChunks);
        _mockMessageRepo.Verify(r => r.CreateAsync(
            It.Is<Message>(m =>
                m.Role == MessageRole.Assistant &&
                m.Content == expectedFullResponse),
            default), Times.Once);
    }

    [Fact]
    public async Task SendMessageStreamAsync_ThrowsInvalidOperationException_WhenSessionNotFound()
    {
        // Arrange
        _mockSessionRepo
            .Setup(r => r.GetByIdAsync(_testSessionId, default))
            .ReturnsAsync((Session?)null);

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
