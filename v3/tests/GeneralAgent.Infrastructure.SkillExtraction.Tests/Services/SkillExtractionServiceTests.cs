using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.SkillExtraction.Tests.Services;

/// <summary>
/// SkillExtractionService 单元测试
/// </summary>
public class SkillExtractionServiceTests
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly ILLMClient _llmClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ILogger<SkillExtractionService> _logger;
    private readonly SkillExtractionService _service;

    public SkillExtractionServiceTests()
    {
        _llmFactory = Substitute.For<ILLMClientFactory>();
        _llmClient = Substitute.For<ILLMClient>();
        _messageRepository = Substitute.For<IMessageRepository>();
        _logger = Substitute.For<ILogger<SkillExtractionService>>();

        _llmFactory.GetClient().Returns(_llmClient);

        _service = new SkillExtractionService(_llmFactory, _messageRepository, _logger);
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_空消息列表_应该返回空列表()
    {
        // Arrange
        var messages = new List<Message>();

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_消息数量不足_应该返回空列表()
    {
        // Arrange
        var messages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = Guid.NewGuid(),
                Role = MessageRole.User,
                Content = "Hello",
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_LLM返回空响应_应该返回空列表()
    {
        // Arrange
        var messages = CreateSampleMessages();
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(""));

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_LLM返回无建议_应该返回空列表()
    {
        // Arrange
        var messages = CreateSampleMessages();
        var llmResponse = """
        {
          "suggestions": []
        }
        """;
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(llmResponse));

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_LLM返回有效建议_应该正确解析()
    {
        // Arrange
        var messages = CreateSampleMessages();
        var llmResponse = """
        {
          "suggestions": [
            {
              "name": "api-helper",
              "namespace": "dev",
              "description": "查看 API 文档并生成示例代码",
              "template": "请帮我完成以下任务：\n1. 查看 {{api}} 的文档\n2. 生成示例代码",
              "parameters": [
                {
                  "name": "api",
                  "type": "string",
                  "required": true,
                  "description": "API 名称"
                }
              ],
              "confidence": 0.85,
              "rationale": "用户多次请求查看 API 文档并生成示例",
              "occurrences": 3,
              "exampleMessages": ["帮我查看用户登录 API 的文档", "生成订单 API 的示例代码"]
            }
          ]
        }
        """;
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(llmResponse));

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().HaveCount(1);
        var suggestion = result[0];
        suggestion.Name.Should().Be("api-helper");
        suggestion.Namespace.Should().Be("dev");
        suggestion.Description.Should().Be("查看 API 文档并生成示例代码");
        suggestion.Template.Should().Contain("{{api}}");
        suggestion.Parameters.Should().HaveCount(1);
        suggestion.Parameters[0].Name.Should().Be("api");
        suggestion.Parameters[0].Required.Should().BeTrue();
        suggestion.Confidence.Should().Be(0.85);
        suggestion.Occurrences.Should().Be(3);
        suggestion.ExampleMessages.Should().HaveCount(2);
        suggestion.FullName.Should().Be("dev:api-helper");
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_低置信度建议_应该被过滤()
    {
        // Arrange
        var messages = CreateSampleMessages();
        var llmResponse = """
        {
          "suggestions": [
            {
              "name": "low-confidence-skill",
              "namespace": "test",
              "description": "低置信度技能",
              "template": "测试模板",
              "parameters": [],
              "confidence": 0.5,
              "rationale": "置信度不足",
              "occurrences": 1,
              "exampleMessages": []
            }
          ]
        }
        """;
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(llmResponse));

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_多个建议_应该按置信度降序排序()
    {
        // Arrange
        var messages = CreateSampleMessages();
        var llmResponse = """
        {
          "suggestions": [
            {
              "name": "skill-1",
              "namespace": "test",
              "description": "Skill 1",
              "template": "Template 1",
              "parameters": [],
              "confidence": 0.7,
              "rationale": "Rationale 1",
              "occurrences": 2,
              "exampleMessages": []
            },
            {
              "name": "skill-2",
              "namespace": "test",
              "description": "Skill 2",
              "template": "Template 2",
              "parameters": [],
              "confidence": 0.9,
              "rationale": "Rationale 2",
              "occurrences": 3,
              "exampleMessages": []
            },
            {
              "name": "skill-3",
              "namespace": "test",
              "description": "Skill 3",
              "template": "Template 3",
              "parameters": [],
              "confidence": 0.8,
              "rationale": "Rationale 3",
              "occurrences": 2,
              "exampleMessages": []
            }
          ]
        }
        """;
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(llmResponse));

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("skill-2");
        result[0].Confidence.Should().Be(0.9);
        result[1].Name.Should().Be("skill-3");
        result[1].Confidence.Should().Be(0.8);
        result[2].Name.Should().Be("skill-1");
        result[2].Confidence.Should().Be(0.7);
    }

    [Fact]
    public async Task ExtractFromMessagesAsync_Markdown代码块包裹的JSON_应该正确提取()
    {
        // Arrange
        var messages = CreateSampleMessages();
        var llmResponse = """
        这是分析结果：

        ```json
        {
          "suggestions": [
            {
              "name": "test-skill",
              "namespace": "test",
              "description": "测试技能",
              "template": "测试模板",
              "parameters": [],
              "confidence": 0.8,
              "rationale": "测试原因",
              "occurrences": 2,
              "exampleMessages": []
            }
          ]
        }
        ```
        """;
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(llmResponse));

        // Act
        var result = await _service.ExtractFromMessagesAsync(messages);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("test-skill");
    }

    [Fact]
    public async Task ExtractFromSessionAsync_无效的会话ID_应该返回空列表()
    {
        // Arrange
        var invalidSessionId = "invalid-guid";

        // Act
        var result = await _service.ExtractFromSessionAsync(invalidSessionId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromSessionAsync_会话无消息_应该返回空列表()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _messageRepository.GetRecentAsync(sessionId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        // Act
        var result = await _service.ExtractFromSessionAsync(sessionId.ToString());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractFromSessionAsync_有效会话_应该加载消息并提取()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var messages = CreateSampleMessages(sessionId);
        _messageRepository.GetRecentAsync(sessionId, 50, Arg.Any<CancellationToken>())
            .Returns(messages);

        var llmResponse = """
        {
          "suggestions": [
            {
              "name": "test-skill",
              "namespace": "test",
              "description": "测试技能",
              "template": "测试模板",
              "parameters": [],
              "confidence": 0.8,
              "rationale": "测试原因",
              "occurrences": 2,
              "exampleMessages": []
            }
          ]
        }
        """;
        _llmClient.CompleteAsync(Arg.Any<CompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateMockResponse(llmResponse));

        // Act
        var result = await _service.ExtractFromSessionAsync(sessionId.ToString());

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("test-skill");
        await _messageRepository.Received(1).GetRecentAsync(sessionId, 50, Arg.Any<CancellationToken>());
    }

    private List<Message> CreateSampleMessages(Guid? sessionId = null)
    {
        var sid = sessionId ?? Guid.NewGuid();
        return new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sid,
                Role = MessageRole.User,
                Content = "帮我查看用户登录 API 的文档",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sid,
                Role = MessageRole.Assistant,
                Content = "好的，我来查看用户登录 API 的文档...",
                CreatedAt = DateTime.UtcNow.AddMinutes(-9)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sid,
                Role = MessageRole.User,
                Content = "帮我生成订单 API 的示例代码",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sid,
                Role = MessageRole.Assistant,
                Content = "好的，我来生成订单 API 的示例代码...",
                CreatedAt = DateTime.UtcNow.AddMinutes(-4)
            }
        };
    }

    private CompletionResponse CreateMockResponse(string content)
    {
        return new CompletionResponse
        {
            Content = content,
            Usage = new TokenUsage
            {
                PromptTokens = 100,
                CompletionTokens = 50
            },
            Timestamp = DateTime.UtcNow
        };
    }
}
