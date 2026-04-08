using FluentAssertions;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.SkillExtraction.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.SkillExtraction.Tests.Repositories;

/// <summary>
/// InMemoryExtractionHistoryRepository 单元测试
/// </summary>
public class InMemoryExtractionHistoryRepositoryTests
{
    private readonly ILogger<InMemoryExtractionHistoryRepository> _logger;
    private readonly InMemoryExtractionHistoryRepository _repository;

    public InMemoryExtractionHistoryRepositoryTests()
    {
        _logger = Substitute.For<ILogger<InMemoryExtractionHistoryRepository>>();
        _repository = new InMemoryExtractionHistoryRepository(_logger);
    }

    [Fact]
    public async Task CreateAsync_新记录_应该成功创建()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act
        var result = await _repository.CreateAsync(record);

        // Assert
        result.Should().Be(record);
        _repository.Count.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_重复ID_应该抛出异常()
    {
        // Arrange
        var record = CreateTestRecord();
        await _repository.CreateAsync(record);

        // Act
        var act = async () => await _repository.CreateAsync(record);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*已存在*");
    }

    [Fact]
    public async Task GetByIdAsync_存在的记录_应该返回记录()
    {
        // Arrange
        var record = CreateTestRecord();
        await _repository.CreateAsync(record);

        // Act
        var result = await _repository.GetByIdAsync(record.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(record.Id);
        result.SkillName.Should().Be(record.SkillName);
    }

    [Fact]
    public async Task GetByIdAsync_不存在的记录_应该返回null()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySessionAsync_应该返回会话的所有记录()
    {
        // Arrange
        var sessionId = Guid.NewGuid().ToString();
        var record1 = CreateTestRecord(sessionId: sessionId, skillName: "skill-1");
        var record2 = CreateTestRecord(sessionId: sessionId, skillName: "skill-2");
        var record3 = CreateTestRecord(sessionId: Guid.NewGuid().ToString(), skillName: "skill-3");

        await _repository.CreateAsync(record1);
        await _repository.CreateAsync(record2);
        await _repository.CreateAsync(record3);

        // Act
        var results = await _repository.GetBySessionAsync(sessionId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.SkillName == "skill-1");
        results.Should().Contain(r => r.SkillName == "skill-2");
        results.Should().NotContain(r => r.SkillName == "skill-3");
    }

    [Fact]
    public async Task GetBySkillAsync_应该返回技能的所有记录()
    {
        // Arrange
        var record1 = CreateTestRecord(skillNamespace: "test", skillName: "skill-1");
        var record2 = CreateTestRecord(skillNamespace: "test", skillName: "skill-1");
        var record3 = CreateTestRecord(skillNamespace: "test", skillName: "skill-2");

        await _repository.CreateAsync(record1);
        await _repository.CreateAsync(record2);
        await _repository.CreateAsync(record3);

        // Act
        var results = await _repository.GetBySkillAsync("test", "skill-1");

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r =>
        {
            r.SkillNamespace.Should().Be("test");
            r.SkillName.Should().Be("skill-1");
        });
    }

    [Fact]
    public async Task GetByActionAsync_应该返回指定动作的记录()
    {
        // Arrange
        var record1 = CreateTestRecord(action: EditAction.Accept);
        var record2 = CreateTestRecord(action: EditAction.Accept);
        var record3 = CreateTestRecord(action: EditAction.Reject);

        await _repository.CreateAsync(record1);
        await _repository.CreateAsync(record2);
        await _repository.CreateAsync(record3);

        // Act
        var results = await _repository.GetByActionAsync(EditAction.Accept);

        // Assert
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Action.Should().Be(EditAction.Accept));
    }

    [Fact]
    public async Task GetStatisticsAsync_应该返回正确的统计信息()
    {
        // Arrange
        await _repository.CreateAsync(CreateTestRecord(action: EditAction.Accept, confidence: 0.8));
        await _repository.CreateAsync(CreateTestRecord(action: EditAction.Accept, confidence: 0.9));
        await _repository.CreateAsync(CreateTestRecord(action: EditAction.Edit, confidence: 0.7));
        await _repository.CreateAsync(CreateTestRecord(action: EditAction.Reject, confidence: 0.5));

        // Act
        var stats = await _repository.GetStatisticsAsync();

        // Assert
        stats.TotalExtractions.Should().Be(4);
        stats.AcceptedCount.Should().Be(2);
        stats.EditedCount.Should().Be(1);
        stats.RejectedCount.Should().Be(1);
        stats.AverageConfidence.Should().BeApproximately(0.725, 0.001);
    }

    [Fact]
    public async Task GetStatisticsAsync_空仓储_应该返回零值()
    {
        // Act
        var stats = await _repository.GetStatisticsAsync();

        // Assert
        stats.TotalExtractions.Should().Be(0);
        stats.AcceptedCount.Should().Be(0);
        stats.EditedCount.Should().Be(0);
        stats.RejectedCount.Should().Be(0);
        stats.AverageConfidence.Should().Be(0);
    }

    [Fact]
    public async Task Clear_应该清空所有记录()
    {
        // Arrange
        await _repository.CreateAsync(CreateTestRecord());
        await _repository.CreateAsync(CreateTestRecord());

        // Act
        _repository.Clear();

        // Assert
        _repository.Count.Should().Be(0);
    }

    private ExtractionRecord CreateTestRecord(
        string? sessionId = null,
        string skillNamespace = "test",
        string skillName = "test-skill",
        EditAction action = EditAction.Accept,
        double confidence = 0.8)
    {
        return new ExtractionRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            SkillName = skillName,
            SkillNamespace = skillNamespace,
            Action = action,
            Confidence = confidence,
            Occurrences = 3,
            RejectionReason = action == EditAction.Reject ? "测试拒绝" : null,
            Metadata = new Dictionary<string, object>()
        };
    }
}
