using FluentAssertions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Models;

public class SessionTests
{
    [Fact]
    public void Create_ShouldGenerateUniqueId()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldSetTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var session = Session.Create();
        var after = DateTime.UtcNow;

        // Assert
        session.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        session.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_WithTitle_ShouldSetTitle()
    {
        // Act
        var session = Session.Create(title: "Test Session");

        // Assert
        session.Title.Should().Be("Test Session");
    }

    [Fact]
    public void Create_WithoutTitle_ShouldHaveNullTitle()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Title.Should().BeNull();
    }

    [Fact]
    public void Create_WithParentId_ShouldBeSubagentType()
    {
        // Arrange
        var parentId = Guid.NewGuid();

        // Act
        var session = Session.Create(parentId: parentId);

        // Assert
        session.Type.Should().Be(SessionType.Subagent);
        session.ParentId.Should().Be(parentId);
    }

    [Fact]
    public void Create_WithoutParentId_ShouldBeNormalType()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Type.Should().Be(SessionType.Normal);
        session.ParentId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldDefaultToActiveStatus()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public void WithTitle_ShouldReturnNewInstanceWithUpdatedTitle()
    {
        // Arrange
        var original = Session.Create(title: "Original");

        // Act
        var updated = original.WithTitle("Updated");

        // Assert
        updated.Should().NotBeSameAs(original);
        updated.Title.Should().Be("Updated");
        updated.Id.Should().Be(original.Id);
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    [Fact]
    public void WithStatus_ShouldReturnNewInstanceWithUpdatedStatus()
    {
        // Arrange
        var original = Session.Create();

        // Act
        var updated = original.WithStatus(SessionStatus.Completed);

        // Assert
        updated.Should().NotBeSameAs(original);
        updated.Status.Should().Be(SessionStatus.Completed);
        updated.Id.Should().Be(original.Id);
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    [Fact]
    public void Session_ShouldBeImmutable()
    {
        // Arrange
        var session = Session.Create(title: "Test");

        // Act & Assert
        // 以下代码不应编译（验证不可变性）
        // session.Title = "Modified";  // 应该报错
        // session.Status = SessionStatus.Completed;  // 应该报错
    }
}
