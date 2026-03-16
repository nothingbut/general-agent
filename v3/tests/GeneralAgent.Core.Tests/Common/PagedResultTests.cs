using FluentAssertions;
using GeneralAgent.Core.Common;

namespace GeneralAgent.Core.Tests.Common;

public class PagedResultTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };

        // Act
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 0);

        // Assert
        result.Items.Should().BeEquivalentTo(items);
        result.Total.Should().Be(10);
        result.Limit.Should().Be(3);
        result.Offset.Should().Be(0);
    }

    [Fact]
    public void HasNextPage_WhenMoreItems_ShouldReturnTrue()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 0);

        // Assert
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenNoMoreItems_ShouldReturnFalse()
    {
        // Arrange
        var items = new List<int> { 8, 9, 10 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 9);

        // Assert
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOffsetIsZero_ShouldReturnFalse()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 0);

        // Assert
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOffsetIsNonZero_ShouldReturnTrue()
    {
        // Arrange
        var items = new List<int> { 4, 5, 6 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 3);

        // Assert
        result.HasPreviousPage.Should().BeTrue();
    }
}
