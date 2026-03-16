using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Tests.Storage;

public class SessionRepositoryTests : IDisposable
{
    private readonly AgentDbContext _context;
    private readonly ISessionRepository _repository;

    public SessionRepositoryTests()
    {
        // 使用内存数据库
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new SessionRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistSession()
    {
        // Arrange
        var session = Session.Create(title: "Test Session");

        // Act
        var created = await _repository.CreateAsync(session);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().Be(session.Id);
        created.Title.Should().Be("Test Session");

        var retrieved = await _repository.GetByIdAsync(session.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Session");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ShouldReturnPagedResults()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            await _repository.CreateAsync(Session.Create(title: $"Session {i}"));
        }

        // Act
        var page1 = await _repository.ListAsync(limit: 10, offset: 0);
        var page2 = await _repository.ListAsync(limit: 10, offset: 10);

        // Assert
        page1.Total.Should().Be(15);
        page1.Items.Count.Should().Be(10);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();

        page2.Total.Should().Be(15);
        page2.Items.Count.Should().Be(5);
        page2.HasNextPage.Should().BeFalse();
        page2.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_ShouldFindMatchingSessions()
    {
        // Arrange
        await _repository.CreateAsync(Session.Create(title: "Project Alpha"));
        await _repository.CreateAsync(Session.Create(title: "Project Beta"));
        await _repository.CreateAsync(Session.Create(title: "Task Alpha"));

        // Act
        var results = await _repository.SearchAsync("Alpha", limit: 10);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(s => s.Title!.Contains("Alpha"));
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        var session = Session.Create(title: "Original");
        await _repository.CreateAsync(session);

        // Act
        var updated = session.WithTitle("Updated");
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetByIdAsync(session.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSession()
    {
        // Arrange
        var session = Session.Create(title: "To Delete");
        await _repository.CreateAsync(session);

        // Act
        await _repository.DeleteAsync(session.Id);

        // Assert
        var retrieved = await _repository.GetByIdAsync(session.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithSubagent_ShouldSetTypeCorrectly()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var subagent = Session.Create(title: "Subagent", parentId: parentId);

        // Act
        await _repository.CreateAsync(subagent);

        // Assert
        var retrieved = await _repository.GetByIdAsync(subagent.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Type.Should().Be(SessionType.Subagent);
        retrieved.ParentId.Should().Be(parentId);
    }
}
