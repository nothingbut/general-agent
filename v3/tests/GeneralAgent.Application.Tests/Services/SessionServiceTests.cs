using FluentAssertions;
using GeneralAgent.Application.Services;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using Moq;

namespace GeneralAgent.Application.Tests.Services;

/// <summary>
/// SessionService 单元测试
/// </summary>
public sealed class SessionServiceTests
{
    private readonly Mock<ISessionRepository> _mockRepository;
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        _mockRepository = new Mock<ISessionRepository>();
        _service = new SessionService(_mockRepository.Object);
    }

    #region CreateSessionAsync Tests

    [Fact]
    public async Task CreateSessionAsync_WithTitle_ShouldCreateSessionSuccessfully()
    {
        // Arrange
        var title = "Test Session";
        var createdSession = Session.Create(title);
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdSession);

        // Act
        var result = await _service.CreateSessionAsync(title);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Type.Should().Be(SessionType.Normal);
        result.Status.Should().Be(SessionStatus.Active);
        result.ParentId.Should().BeNull();
        _mockRepository.Verify(
            r => r.CreateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_WithoutTitle_ShouldCreateSessionWithoutTitle()
    {
        // Arrange
        var createdSession = Session.Create();
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdSession);

        // Act
        var result = await _service.CreateSessionAsync(null);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().BeNull();
        result.Type.Should().Be(SessionType.Normal);
        _mockRepository.Verify(
            r => r.CreateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_WithParentId_ShouldCreateSubagentSession()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var title = "Subagent Session";
        var createdSession = Session.Create(title, parentId);
        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdSession);

        // Act
        var result = await _service.CreateSessionAsync(title, parentId);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.ParentId.Should().Be(parentId);
        result.Type.Should().Be(SessionType.Subagent);
        _mockRepository.Verify(
            r => r.CreateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetSessionAsync Tests

    [Fact]
    public async Task GetSessionAsync_WithValidId_ShouldReturnSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var session = Session.Create("Test Session") with { Id = sessionId };
        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _service.GetSessionAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(sessionId);
        result.Title.Should().Be("Test Session");
        _mockRepository.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSessionAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act
        var result = await _service.GetSessionAsync(sessionId);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region ListSessionsAsync Tests

    [Fact]
    public async Task ListSessionsAsync_ShouldReturnPagedSessions()
    {
        // Arrange
        var session1 = Session.Create("Session 1");
        var session2 = Session.Create("Session 2");
        var sessions = new List<Session> { session1, session2 };
        var pagedResult = new PagedResult<Session>(sessions, 2, 20, 0);

        _mockRepository
            .Setup(r => r.ListAsync(20, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.ListSessionsAsync(limit: 20, offset: 0);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Limit.Should().Be(20);
        result.Offset.Should().Be(0);
        _mockRepository.Verify(
            r => r.ListAsync(20, 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListSessionsAsync_WithCustomPagination_ShouldRespectLimitAndOffset()
    {
        // Arrange
        var session = Session.Create("Session 1");
        var sessions = new List<Session> { session };
        var pagedResult = new PagedResult<Session>(sessions, 10, 5, 5);

        _mockRepository
            .Setup(r => r.ListAsync(5, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.ListSessionsAsync(limit: 5, offset: 5);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Limit.Should().Be(5);
        result.Offset.Should().Be(5);
        _mockRepository.Verify(
            r => r.ListAsync(5, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ListSessionsAsync_WithEmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        var sessions = new List<Session>();
        var pagedResult = new PagedResult<Session>(sessions, 0, 20, 0);

        _mockRepository
            .Setup(r => r.ListAsync(20, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.ListSessionsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    #endregion

    #region UpdateSessionTitleAsync Tests

    [Fact]
    public async Task UpdateSessionTitleAsync_WithValidIdAndNewTitle_ShouldUpdateSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var originalSession = Session.Create("Original Title") with { Id = sessionId };
        var updatedSession = originalSession.WithTitle("New Title");

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSessionTitleAsync(sessionId, "New Title");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("New Title");
        result.Id.Should().Be(sessionId);
        _mockRepository.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSessionTitleAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateSessionTitleAsync(sessionId, "New Title"));
        exception.Message.Should().Contain("会话不存在");
    }

    [Fact]
    public async Task UpdateSessionTitleAsync_ClearingTitle_ShouldSetTitleToNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var originalSession = Session.Create("Original Title") with { Id = sessionId };
        var updatedSession = originalSession.WithTitle(null);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSessionTitleAsync(sessionId, null);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().BeNull();
    }

    #endregion

    #region UpdateSessionStatusAsync Tests

    [Fact]
    public async Task UpdateSessionStatusAsync_WithValidIdAndNewStatus_ShouldUpdateSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var originalSession = Session.Create() with { Id = sessionId, Status = SessionStatus.Active };
        var updatedSession = originalSession.WithStatus(SessionStatus.Running);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSessionStatusAsync(sessionId, SessionStatus.Running);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(SessionStatus.Running);
        result.Id.Should().Be(sessionId);
        _mockRepository.Verify(
            r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSessionStatusAsync_WithInvalidId_ShouldThrowException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateSessionStatusAsync(sessionId, SessionStatus.Completed));
        exception.Message.Should().Contain("会话不存在");
    }

    [Theory]
    [InlineData(SessionStatus.Active)]
    [InlineData(SessionStatus.Running)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Failed)]
    public async Task UpdateSessionStatusAsync_ShouldSupportAllStatusTransitions(SessionStatus newStatus)
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var originalSession = Session.Create() with { Id = sessionId };
        var updatedSession = originalSession.WithStatus(newStatus);

        _mockRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalSession);
        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateSessionStatusAsync(sessionId, newStatus);

        // Assert
        result.Status.Should().Be(newStatus);
    }

    #endregion

    #region DeleteSessionAsync Tests

    [Fact]
    public async Task DeleteSessionAsync_WithValidId_ShouldDeleteSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.DeleteAsync(sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteSessionAsync(sessionId);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteSessionAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        _mockRepository
            .Setup(r => r.DeleteAsync(sessionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteSessionAsync(sessionId, cts.Token);

        // Assert
        _mockRepository.Verify(
            r => r.DeleteAsync(sessionId, cts.Token),
            Times.Once);
    }

    #endregion
}
