using FluentAssertions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Models;

public class SearchQueryTests
{
    [Fact]
    public void SearchCriteria_EmptyConstructor_InitializesLists()
    {
        // Act
        var criteria = new SearchCriteria();

        // Assert
        criteria.Keywords.Should().NotBeNull();
        criteria.ExactPhrases.Should().NotBeNull();
        criteria.Keywords.Should().BeEmpty();
        criteria.ExactPhrases.Should().BeEmpty();
    }

    [Fact]
    public void SearchCriteria_WithKeywords_StoresKeywords()
    {
        // Arrange
        var keywords = new List<string> { "python", "bug" };

        // Act
        var criteria = new SearchCriteria
        {
            Keywords = keywords
        };

        // Assert
        criteria.Keywords.Should().HaveCount(2);
        criteria.Keywords.Should().Contain("python");
    }

    [Fact]
    public void SearchQuery_DefaultType_IsKeyword()
    {
        // Act
        var query = new SearchQuery
        {
            NaturalQuery = "test query"
        };

        // Assert
        query.Type.Should().Be(SearchType.Keyword);
    }
}
