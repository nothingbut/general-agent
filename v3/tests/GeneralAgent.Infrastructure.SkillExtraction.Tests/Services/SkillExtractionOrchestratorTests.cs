using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using GeneralAgent.Infrastructure.Skills.Parsers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Infrastructure.SkillExtraction.Tests.Services;

/// <summary>
/// SkillExtractionOrchestrator 单元测试
/// </summary>
public class SkillExtractionOrchestratorTests : IDisposable
{
    private readonly ISkillExtractionService _extractionService;
    private readonly ISkillGenerator _skillGenerator;
    private readonly ISkillWriter _skillWriter;
    private readonly TestUserInteraction _userInteraction;
    private readonly IExtractionHistoryRepository _historyRepository;
    private readonly ILogger<SkillExtractionOrchestrator> _logger;
    private readonly SkillExtractionOrchestrator _orchestrator;
    private readonly ILLMClientFactory _llmFactory;
    private readonly ILLMClient _llmClient;
    private readonly IMessageRepository _messageRepository;
    private readonly string _testSkillsDirectory;

    public SkillExtractionOrchestratorTests()
    {
        // 设置 LLM 客户端 mock
        _llmFactory = Substitute.For<ILLMClientFactory>();
        _llmClient = Substitute.For<ILLMClient>();
        _llmFactory.GetClient().Returns(_llmClient);

        // 设置消息仓储 mock
        _messageRepository = Substitute.For<IMessageRepository>();

        // 创建真实的服务实例
        var extractionLogger = Substitute.For<ILogger<SkillExtractionService>>();
        _extractionService = new SkillExtractionService(
            _llmFactory, _messageRepository, extractionLogger);

        var skillParser = new MarkdownSkillParser();
        var generatorLogger = Substitute.For<ILogger<SkillGenerator>>();
        _skillGenerator = new SkillGenerator(skillParser, generatorLogger);

        // 创建临时测试目录
        _testSkillsDirectory = Path.Combine(Path.GetTempPath(), $"test-skills-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSkillsDirectory);

        var writerOptions = Options.Create(new SkillExtractionOptions
        {
            SkillsDirectory = _testSkillsDirectory,
            AutoCreateNamespaceDirectory = true,
            OverwriteExisting = false
        });
        var writerLogger = Substitute.For<ILogger<SkillWriter>>();
        _skillWriter = new SkillWriter(writerOptions, writerLogger);

        _userInteraction = new TestUserInteraction();

        var historyLogger = Substitute.For<ILogger<InMemoryExtractionHistoryRepository>>();
        _historyRepository = new InMemoryExtractionHistoryRepository(historyLogger);

        _logger = Substitute.For<ILogger<SkillExtractionOrchestrator>>();

        _orchestrator = new SkillExtractionOrchestrator(
            _extractionService,
            _skillGenerator,
            _skillWriter,
            _userInteraction,
            _historyRepository,
            _logger);
    }

    public void Dispose()
    {
        // 清理测试目录
        if (Directory.Exists(_testSkillsDirectory))
        {
            Directory.Delete(_testSkillsDirectory, true);
        }
    }

    [Fact]
    public async Task CreateSkillFromSuggestionAsync_用户接受_应该保存技能()
    {
        // Arrange
        var suggestion = CreateTestSuggestion("greeting", "personal");

        _userInteraction.ConfigureNextAction(new EditResult
        {
            Action = EditAction.Accept
        });

        // Act
        var result = await _orchestrator.CreateSkillFromSuggestionAsync(suggestion);

        // Assert
        result.Should().NotBeNull();
        File.Exists(result).Should().BeTrue();
        _userInteraction.Successes.Should().Contain(s => s.Contains("已保存"));
    }

    [Fact]
    public async Task CreateSkillFromSuggestionAsync_用户拒绝_应该返回null()
    {
        // Arrange
        var suggestion = CreateTestSuggestion("test-skill", "test");

        _userInteraction.ConfigureNextAction(new EditResult
        {
            Action = EditAction.Reject,
            RejectionReason = "不需要此技能"
        });

        // Act
        var result = await _orchestrator.CreateSkillFromSuggestionAsync(suggestion);

        // Assert
        result.Should().BeNull();
        _userInteraction.Messages.Should().Contain(s => s.Contains("已拒绝"));
    }

    [Fact]
    public async Task CreateSkillFromSuggestionAsync_用户编辑并保存_应该保存编辑后的内容()
    {
        // Arrange
        var suggestion = CreateTestSuggestion("edited-skill", "test");

        var editedContent = """
        ---
        name: edited-skill
        description: 编辑后的技能
        parameters: []
        ---

        这是编辑后的内容
        """;

        _userInteraction.ConfigureNextAction(new EditResult
        {
            Action = EditAction.Edit,
            EditedContent = editedContent
        });

        _userInteraction.ConfigureNextEdit(editedContent);

        // Act
        var result = await _orchestrator.CreateSkillFromSuggestionAsync(suggestion);

        // Assert
        result.Should().NotBeNull();
        var savedContent = await File.ReadAllTextAsync(result!);
        savedContent.Should().Be(editedContent);
    }

    [Fact]
    public async Task CreateSkillFromSuggestionAsync_编辑后验证失败_应该返回null()
    {
        // Arrange
        var suggestion = CreateTestSuggestion("invalid-skill", "test");

        var invalidContent = "这是无效的技能内容（缺少 frontmatter）";

        _userInteraction.ConfigureNextAction(new EditResult
        {
            Action = EditAction.Edit
        });

        _userInteraction.ConfigureNextEdit(invalidContent);

        // Act
        var result = await _orchestrator.CreateSkillFromSuggestionAsync(suggestion);

        // Assert
        result.Should().BeNull();
        _userInteraction.Errors.Should().Contain(e => e.Contains("无效"));
    }

    [Fact]
    public async Task CreateSkillFromSuggestionAsync_用户取消编辑_应该返回null()
    {
        // Arrange
        var suggestion = CreateTestSuggestion("cancelled-skill", "test");

        _userInteraction.ConfigureNextAction(new EditResult
        {
            Action = EditAction.Edit
        });

        _userInteraction.ConfigureNextEdit(null); // 用户取消

        // Act
        var result = await _orchestrator.CreateSkillFromSuggestionAsync(suggestion);

        // Assert
        result.Should().BeNull();
        _userInteraction.Messages.Should().Contain(m => m.Contains("已取消"));
    }

    [Fact]
    public async Task ExtractAndCreateFromSessionAsync_无建议_应该返回空列表()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();

        // Mock 空的建议列表
        _messageRepository.GetRecentAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Message>());

        // Act
        var results = await _orchestrator.ExtractAndCreateFromSessionAsync(sessionId);

        // Assert
        results.Should().BeEmpty();
        _userInteraction.Messages.Should().Contain(m => m.Contains("未发现"));
    }

    [Fact]
    public async Task CreateSkillFromSuggestionAsync_应该记录历史()
    {
        // Arrange
        var suggestion = CreateTestSuggestion("history-test", "test");
        var sessionId = Guid.NewGuid().ToString();

        _userInteraction.ConfigureNextAction(new EditResult
        {
            Action = EditAction.Accept
        });

        // Act
        await _orchestrator.CreateSkillFromSuggestionAsync(suggestion, sessionId);

        // Assert
        var history = await _historyRepository.GetBySessionAsync(sessionId);
        history.Should().HaveCount(1);
        history[0].SkillName.Should().Be("history-test");
        history[0].Action.Should().Be(EditAction.Accept);
    }

    private SkillSuggestion CreateTestSuggestion(
        string name = "test-skill",
        string @namespace = "test")
    {
        return new SkillSuggestion
        {
            Name = name,
            Description = "测试技能描述",
            Namespace = @namespace,
            Template = "这是一个测试技能模板",
            Parameters = new List<SkillParameterDefinition>(),
            Confidence = 0.8,
            Rationale = "测试原因",
            Occurrences = 3,
            ExampleMessages = new List<string> { "示例1", "示例2" }
        };
    }
}
