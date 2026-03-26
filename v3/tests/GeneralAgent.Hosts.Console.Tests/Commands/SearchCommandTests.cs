using GeneralAgent.Hosts.Console.Commands;
using GeneralAgent.Hosts.Console.Services;
using NSubstitute;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// SearchCommand 测试
/// </summary>
public class SearchCommandTests
{
    private readonly ISearchService _mockSearchService;
    private readonly SearchCommand _command;

    public SearchCommandTests()
    {
        _mockSearchService = Substitute.For<ISearchService>();
        _command = new SearchCommand(_mockSearchService);
    }

    [Fact]
    public async Task ExecuteAsync_WithNaturalQuery_CallsSearchService()
    {
        // Arrange
        var query = "查找昨天关于 Python 的讨论";
        var searchResults = new List<SearchResult>
        {
            new(Guid.NewGuid(), "Python 讨论", "相关内容", DateTime.UtcNow)
        };
        _mockSearchService
            .SearchWithNaturalLanguageAsync(query, Arg.Any<CancellationToken>())
            .Returns(searchResults);

        // Act
        await _command.ExecuteAsync(query);

        // Assert
        await _mockSearchService.Received(1).SearchWithNaturalLanguageAsync(
            query,
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_NoResults_DoesNotThrow()
    {
        // Arrange
        var query = "不存在的查询";
        _mockSearchService
            .SearchWithNaturalLanguageAsync(query, Arg.Any<CancellationToken>())
            .Returns(new List<SearchResult>());

        // Act & Assert (不应抛出异常)
        await _command.ExecuteAsync(query);

        await _mockSearchService.Received(1).SearchWithNaturalLanguageAsync(
            query,
            Arg.Any<CancellationToken>()
        );
    }
}
