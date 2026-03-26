using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Tests.Storage.Repositories;

/// <summary>
/// SessionTagRepository 测试
/// </summary>
public class SessionTagRepositoryTests : IDisposable
{
    private readonly AgentDbContext _context;
    private readonly ISessionTagRepository _repository;

    public SessionTagRepositoryTests()
    {
        // 使用内存数据库
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new SessionTagRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistTag()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tag = SessionTag.Create(
            sessionId: sessionId,
            tag: "Important",
            source: TagSource.User,
            color: "#FF5733",
            emoji: "⭐"
        );

        // Act
        await _repository.AddAsync(tag);

        // Assert
        var tags = await _repository.GetBySessionAsync(sessionId);
        tags.Should().ContainSingle();
        tags[0].Tag.Should().Be("important"); // 应该小写
        tags[0].Color.Should().Be("#FF5733");
        tags[0].Emoji.Should().Be("⭐");
        tags[0].Source.Should().Be(TagSource.User);
    }

    [Fact]
    public async Task AddAsync_WhenDuplicateTag_ShouldThrowStorageException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tag1 = SessionTag.Create(sessionId, "duplicate");
        var tag2 = SessionTag.Create(sessionId, "duplicate"); // 同一会话相同标签

        // Act
        await _repository.AddAsync(tag1);
        var act = async () => await _repository.AddAsync(tag2);

        // Assert
        await act.Should().ThrowAsync<StorageException>()
            .WithMessage("*duplicate*");
    }

    [Fact]
    public async Task GetBySessionAsync_ShouldReturnAllTags()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tag1 = SessionTag.Create(sessionId, "bug");
        var tag2 = SessionTag.Create(sessionId, "feature");
        var tag3 = SessionTag.Create(sessionId, "urgent");

        await _repository.AddAsync(tag1);
        await _repository.AddAsync(tag2);
        await _repository.AddAsync(tag3);

        // 添加其他会话的标签（不应返回）
        var otherSessionId = Guid.NewGuid();
        await _repository.AddAsync(SessionTag.Create(otherSessionId, "other"));

        // Act
        var tags = await _repository.GetBySessionAsync(sessionId);

        // Assert
        tags.Should().HaveCount(3);
        tags.Should().Contain(t => t.Tag == "bug");
        tags.Should().Contain(t => t.Tag == "feature");
        tags.Should().Contain(t => t.Tag == "urgent");
        tags.Should().NotContain(t => t.Tag == "other");
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteTag()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tag = SessionTag.Create(sessionId, "ToDelete");
        await _repository.AddAsync(tag);

        // Act - 使用大写删除，应该匹配小写存储的标签
        await _repository.RemoveAsync(sessionId, "ToDelete");

        // Assert
        var tags = await _repository.GetBySessionAsync(sessionId);
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_WhenTagNotExists_ShouldNotThrow()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var act = async () => await _repository.RemoveAsync(sessionId, "nonexistent");

        // Assert - 不应抛出异常
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByTagAsync_ShouldReturnMatchingSessions()
    {
        // Arrange
        var session1 = Guid.NewGuid();
        var session2 = Guid.NewGuid();
        var session3 = Guid.NewGuid();

        await _repository.AddAsync(SessionTag.Create(session1, "bug"));
        await _repository.AddAsync(SessionTag.Create(session2, "bug"));
        await _repository.AddAsync(SessionTag.Create(session3, "feature"));

        // Act - 大小写不敏感
        var sessionIds = await _repository.GetByTagAsync("BUG");

        // Assert
        sessionIds.Should().HaveCount(2);
        sessionIds.Should().Contain(session1);
        sessionIds.Should().Contain(session2);
        sessionIds.Should().NotContain(session3);
    }

    [Fact]
    public async Task GetAllTagsAsync_ShouldReturnDistinctTags()
    {
        // Arrange
        var session1 = Guid.NewGuid();
        var session2 = Guid.NewGuid();

        await _repository.AddAsync(SessionTag.Create(session1, "bug"));
        await _repository.AddAsync(SessionTag.Create(session1, "feature"));
        await _repository.AddAsync(SessionTag.Create(session2, "bug")); // 重复标签
        await _repository.AddAsync(SessionTag.Create(session2, "urgent"));

        // Act
        var tags = await _repository.GetAllTagsAsync();

        // Assert
        tags.Should().HaveCount(3); // bug, feature, urgent（去重）
        tags.Should().Contain("bug");
        tags.Should().Contain("feature");
        tags.Should().Contain("urgent");
    }

    [Fact]
    public async Task GetTagStatisticsAsync_ShouldReturnTagCounts()
    {
        // Arrange
        var session1 = Guid.NewGuid();
        var session2 = Guid.NewGuid();
        var session3 = Guid.NewGuid();

        // "python" 标签在 2 个会话中使用
        await _repository.AddAsync(SessionTag.Create(session1, "python"));
        await _repository.AddAsync(SessionTag.Create(session2, "python"));

        // "bug" 标签在 3 个会话中使用
        await _repository.AddAsync(SessionTag.Create(session1, "bug"));
        await _repository.AddAsync(SessionTag.Create(session2, "bug"));
        await _repository.AddAsync(SessionTag.Create(session3, "bug"));

        // "feature" 标签在 1 个会话中使用
        await _repository.AddAsync(SessionTag.Create(session3, "feature"));

        // Act
        var statistics = await _repository.GetTagStatisticsAsync();

        // Assert
        statistics.Should().HaveCount(3);
        statistics["python"].Should().Be(2);
        statistics["bug"].Should().Be(3);
        statistics["feature"].Should().Be(1);
    }

    [Fact]
    public async Task RemoveBySessionAsync_ShouldDeleteAllSessionTags()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        await _repository.AddAsync(SessionTag.Create(sessionId, "tag1"));
        await _repository.AddAsync(SessionTag.Create(sessionId, "tag2"));
        await _repository.AddAsync(SessionTag.Create(sessionId, "tag3"));

        // 添加其他会话的标签（不应删除）
        var otherSessionId = Guid.NewGuid();
        await _repository.AddAsync(SessionTag.Create(otherSessionId, "keep"));

        // Act
        await _repository.RemoveBySessionAsync(sessionId);

        // Assert
        var tags = await _repository.GetBySessionAsync(sessionId);
        tags.Should().BeEmpty();

        var otherTags = await _repository.GetBySessionAsync(otherSessionId);
        otherTags.Should().ContainSingle();
    }
}
