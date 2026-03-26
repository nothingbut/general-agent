using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Hosts.Console.Commands;
using NSubstitute;

namespace GeneralAgent.Hosts.Console.Tests.Commands;

/// <summary>
/// TagCommand 测试
/// </summary>
public class TagCommandTests
{
    private readonly ISessionTagRepository _mockTagRepo;
    private readonly ISmartTagService _mockTagService;
    private readonly TagCommand _command;

    public TagCommandTests()
    {
        _mockTagRepo = Substitute.For<ISessionTagRepository>();
        _mockTagService = Substitute.For<ISmartTagService>();
        _command = new TagCommand(_mockTagRepo, _mockTagService);
    }

    [Fact]
    public async Task ExecuteAddAsync_ValidTag_AddsSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tagName = "python";
        var emoji = "🐍";
        var color = "#3776AB";

        // Act
        await _command.ExecuteAddAsync(sessionId, tagName, emoji, color, CancellationToken.None);

        // Assert
        await _mockTagRepo.Received(1).AddAsync(
            Arg.Is<SessionTag>(t =>
                t.Tag == "python" &&
                t.Source == TagSource.User &&
                t.SessionId == sessionId &&
                t.Emoji == emoji &&
                t.Color == color
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteRemoveAsync_ExistingTag_RemovesSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tagName = "python";

        // Act
        await _command.ExecuteRemoveAsync(sessionId, tagName, CancellationToken.None);

        // Assert
        await _mockTagRepo.Received(1).RemoveAsync(
            sessionId,
            "python",
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteListAsync_WithTags_DisplaysTags()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var tags = new List<SessionTag>
        {
            SessionTag.Create(sessionId, "python", TagSource.User, "#3776AB", "🐍"),
            SessionTag.Create(sessionId, "async", TagSource.Auto, "#F59E0B", "⚡")
        };
        _mockTagRepo.GetBySessionAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(tags);

        // Act
        await _command.ExecuteListAsync(sessionId, CancellationToken.None);

        // Assert - 简化测试（仅验证调用）
        await _mockTagRepo.Received(1).GetBySessionAsync(sessionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteSuggestAsync_WithSuggestions_CallsSuggestFromTitleAsync()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionTitle = "Python 数据分析项目";
        var suggestions = new List<TagSuggestion>
        {
            new("python", "🐍", "#3776AB"),
            new("data", "📊", "#FF6B6B")
        };
        _mockTagService
            .SuggestFromTitleAsync(sessionTitle, Arg.Any<CancellationToken>())
            .Returns(suggestions);

        // Act
        await _command.ExecuteSuggestAsync(sessionId, sessionTitle);

        // Assert
        await _mockTagService.Received(1).SuggestFromTitleAsync(
            sessionTitle,
            Arg.Any<CancellationToken>()
        );
        // 注意: AnsiConsole.Confirm 无法 Mock，所以无法验证 ApplySuggestionsAsync 调用
        // 这个测试主要验证 SuggestFromTitleAsync 被调用
    }

    [Fact]
    public async Task ExecuteListAsync_NullSessionId_CallsGetTagStatistics()
    {
        // Arrange
        var statistics = new Dictionary<string, int>
        {
            { "python", 5 },
            { "bug", 3 }
        };
        _mockTagRepo
            .GetTagStatisticsAsync(Arg.Any<CancellationToken>())
            .Returns(statistics);

        // Act
        await _command.ExecuteListAsync(null);

        // Assert
        await _mockTagRepo.Received(1).GetTagStatisticsAsync(
            Arg.Any<CancellationToken>()
        );
    }
}
