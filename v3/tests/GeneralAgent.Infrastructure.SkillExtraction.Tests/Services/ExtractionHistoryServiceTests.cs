using FluentAssertions;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using GeneralAgent.Infrastructure.SkillExtraction.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.SkillExtraction.Tests.Services;

/// <summary>
/// ExtractionHistoryService 单元测试
/// </summary>
public class ExtractionHistoryServiceTests
{
    private readonly InMemoryExtractionHistoryRepository _repository;
    private readonly ILogger<ExtractionHistoryService> _logger;
    private readonly ExtractionHistoryService _service;

    public ExtractionHistoryServiceTests()
    {
        var repositoryLogger = Substitute.For<ILogger<InMemoryExtractionHistoryRepository>>();
        _repository = new InMemoryExtractionHistoryRepository(repositoryLogger);

        _logger = Substitute.For<ILogger<ExtractionHistoryService>>();
        _service = new ExtractionHistoryService(_repository, _logger);
    }

    [Fact]
    public async Task RecordExtractionAsync_应该成功记录()
    {
        // Arrange
        var suggestion = CreateTestSuggestion();

        // Act
        var recordId = await _service.RecordExtractionAsync(
            suggestion, EditAction.Accept);

        // Assert
        recordId.Should().NotBeEmpty();
        _repository.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetHistoryAsync_应该返回限制数量的记录()
    {
        // Arrange
        for (int i = 0; i < 20; i++)
        {
            var suggestion = CreateTestSuggestion($"skill-{i}");
            await _service.RecordExtractionAsync(suggestion, EditAction.Accept);
        }

        // Act
        var history = await _service.GetHistoryAsync(limit: 10);

        // Assert
        history.Should().HaveCount(10);
        history.Should().BeInDescendingOrder(r => r.Timestamp);
    }

    [Fact]
    public async Task GetHistoryByActionAsync_应该只返回指定动作的记录()
    {
        // Arrange
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-2"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-3"), EditAction.Reject);

        // Act
        var acceptedHistory = await _service.GetHistoryByActionAsync(EditAction.Accept);

        // Assert
        acceptedHistory.Should().HaveCount(2);
        acceptedHistory.Should().AllSatisfy(r => r.Action.Should().Be(EditAction.Accept));
    }

    [Fact]
    public async Task GetHistoryBySessionAsync_应该只返回指定会话的记录()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept, sessionId);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-2"), EditAction.Accept, sessionId);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-3"), EditAction.Accept, Guid.NewGuid().ToString());

        // Act
        var sessionHistory = await _service.GetHistoryBySessionAsync(sessionId);

        // Assert
        sessionHistory.Should().HaveCount(2);
        sessionHistory.Should().AllSatisfy(r => r.SessionId.Should().Be(sessionId));
    }

    [Fact]
    public async Task GetHistoryBySkillAsync_应该只返回指定技能的记录()
    {
        // Arrange
        await _service.RecordExtractionAsync(CreateTestSuggestion("test-skill", "test"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("test-skill", "test"), EditAction.Edit);
        await _service.RecordExtractionAsync(CreateTestSuggestion("other-skill", "test"), EditAction.Accept);

        // Act
        var skillHistory = await _service.GetHistoryBySkillAsync("test", "test-skill");

        // Assert
        skillHistory.Should().HaveCount(2);
        skillHistory.Should().AllSatisfy(r =>
        {
            r.SkillNamespace.Should().Be("test");
            r.SkillName.Should().Be("test-skill");
        });
    }

    [Fact]
    public async Task GetStatisticsAsync_应该返回正确的统计信息()
    {
        // Arrange
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-2"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-3"), EditAction.Edit);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-4"), EditAction.Reject);

        // Act
        var stats = await _service.GetStatisticsAsync();

        // Assert
        stats.TotalExtractions.Should().Be(4);
        stats.AcceptedCount.Should().Be(2);
        stats.EditedCount.Should().Be(1);
        stats.RejectedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMostPopularSkillsAsync_应该按接受次数排序()
    {
        // Arrange
        // skill-1: 3次接受
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);

        // skill-2: 2次接受 + 1次编辑
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-2"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-2"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-2"), EditAction.Edit);

        // skill-3: 1次接受
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-3"), EditAction.Accept);

        // Act
        var popularSkills = await _service.GetMostPopularSkillsAsync(limit: 3);

        // Assert
        popularSkills.Should().HaveCount(3);
        popularSkills[0].FullSkillName.Should().Be("test:skill-2"); // 3次（2+1）
        popularSkills[0].AcceptedCount.Should().Be(2);
        popularSkills[0].EditedCount.Should().Be(1);
        popularSkills[1].FullSkillName.Should().Be("test:skill-1"); // 3次
        popularSkills[2].FullSkillName.Should().Be("test:skill-3"); // 1次
    }

    [Fact]
    public async Task GetRejectionPatternsAsync_应该返回拒绝统计()
    {
        // Arrange
        await _service.RecordExtractionAsync(CreateTestSuggestion("rejected-skill"), EditAction.Reject,
            rejectionReason: "不需要");
        await _service.RecordExtractionAsync(CreateTestSuggestion("rejected-skill"), EditAction.Reject,
            rejectionReason: "不需要");
        await _service.RecordExtractionAsync(CreateTestSuggestion("rejected-skill"), EditAction.Reject,
            rejectionReason: "太复杂");

        // Act
        var rejectionPatterns = await _service.GetRejectionPatternsAsync(limit: 10);

        // Assert
        rejectionPatterns.Should().HaveCount(1);
        var pattern = rejectionPatterns[0];
        pattern.FullSkillName.Should().Be("test:rejected-skill");
        pattern.RejectionCount.Should().Be(3);
        pattern.CommonReasons.Should().Contain("不需要");
        pattern.CommonReasons.Should().Contain("太复杂");
    }

    [Fact]
    public async Task RecordExtractionAsync_应该包含元数据()
    {
        // Arrange
        var suggestion = CreateTestSuggestion();
        suggestion = suggestion with
        {
            Parameters = new List<SkillParameterDefinition>
            {
                new SkillParameterDefinition
                {
                    Name = "param1",
                    Type = "string",
                    Required = true,
                    Description = "参数1"
                }
            }
        };

        // Act
        await _service.RecordExtractionAsync(suggestion, EditAction.Accept);

        // Assert
        var history = await _service.GetHistoryAsync(limit: 1);
        var record = history[0];
        record.Metadata.Should().NotBeNull();
        record.Metadata!.Should().ContainKey("Rationale");
        record.Metadata!.Should().ContainKey("ExampleMessages");
        record.Metadata!.Should().ContainKey("ParameterCount");
        record.Metadata!["ParameterCount"].Should().Be(1);
    }

    [Fact]
    public async Task GetMostPopularSkillsAsync_应该计算正确的接受率()
    {
        // Arrange
        // skill-1: 2次接受，1次拒绝 = 66.7% 接受率
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Accept);
        await _service.RecordExtractionAsync(CreateTestSuggestion("skill-1"), EditAction.Reject);

        // Act
        var popularSkills = await _service.GetMostPopularSkillsAsync(limit: 10);

        // Assert
        popularSkills.Should().HaveCount(1);
        var skill = popularSkills[0];
        skill.TotalSuggestions.Should().Be(3);
        skill.AcceptanceRate.Should().BeApproximately(0.6667, 0.001);
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
