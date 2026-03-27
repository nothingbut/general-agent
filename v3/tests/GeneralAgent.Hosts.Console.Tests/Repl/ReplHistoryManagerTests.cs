using FluentAssertions;
using GeneralAgent.Hosts.Console.Repl;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace GeneralAgent.Hosts.Console.Tests.Repl;

public class ReplHistoryManagerTests : IDisposable
{
    private readonly string _testHistoryPath;
    private readonly ILogger<ReplHistoryManager> _logger;

    public ReplHistoryManagerTests()
    {
        // 使用临时文件路径
        _testHistoryPath = Path.Combine(Path.GetTempPath(), $"test_history_{Guid.NewGuid()}.txt");
        _logger = Substitute.For<ILogger<ReplHistoryManager>>();
    }

    public void Dispose()
    {
        // 清理测试文件
        if (File.Exists(_testHistoryPath))
        {
            File.Delete(_testHistoryPath);
        }

        var directory = Path.GetDirectoryName(_testHistoryPath);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            var testFiles = Directory.GetFiles(directory, "test_history_*.txt");
            foreach (var file in testFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // 忽略删除失败
                }
            }
        }
    }

    [Fact]
    public void Constructor_ShouldCreateHistoryManager()
    {
        // Act
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Assert
        manager.Should().NotBeNull();
        manager.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var newPath = Path.Combine(Path.GetTempPath(), "newdir", "history.txt");

        try
        {
            // Act
            var manager = new ReplHistoryManager(newPath, logger: _logger);

            // Assert
            Directory.Exists(Path.GetDirectoryName(newPath)).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(newPath))
            {
                File.Delete(newPath);
            }
            var dir = Path.GetDirectoryName(newPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Directory.Delete(dir);
            }
        }
    }

    [Fact]
    public void AddHistoryItem_ShouldAddItemToHistory()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        manager.AddHistoryItem("/new test");
        manager.AddHistoryItem("你好");

        // Assert
        manager.Count.Should().Be(2);
        var history = manager.GetAllHistory();
        history[0].Should().Be("/new test");
        history[1].Should().Be("你好");
    }

    [Fact]
    public void AddHistoryItem_ShouldIgnoreEmptyOrWhitespaceCommands()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        manager.AddHistoryItem("");
        manager.AddHistoryItem("   ");
        manager.AddHistoryItem(null!);

        // Assert
        manager.Count.Should().Be(0);
    }

    [Fact]
    public void AddHistoryItem_ShouldAvoidConsecutiveDuplicates()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        manager.AddHistoryItem("/list");
        manager.AddHistoryItem("/list");
        manager.AddHistoryItem("/new test");
        manager.AddHistoryItem("/list");

        // Assert
        manager.Count.Should().Be(3);
        var history = manager.GetAllHistory();
        history[0].Should().Be("/list");
        history[1].Should().Be("/new test");
        history[2].Should().Be("/list");
    }

    [Fact]
    public void AddHistoryItem_ShouldPersistToFile()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        manager.AddHistoryItem("/new session");
        manager.AddHistoryItem("hello");

        // Assert
        File.Exists(_testHistoryPath).Should().BeTrue();
        var lines = File.ReadAllLines(_testHistoryPath);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("/new session");
        lines[1].Should().Be("hello");
    }

    [Fact]
    public void AddHistoryItem_ShouldRespectMaxHistorySize()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, maxHistorySize: 3, logger: _logger);

        // Act
        manager.AddHistoryItem("item1");
        manager.AddHistoryItem("item2");
        manager.AddHistoryItem("item3");
        manager.AddHistoryItem("item4");
        manager.AddHistoryItem("item5");

        // Assert
        manager.Count.Should().Be(3);
        var history = manager.GetAllHistory();
        history[0].Should().Be("item3");
        history[1].Should().Be("item4");
        history[2].Should().Be("item5");
    }

    [Fact]
    public void LoadHistory_ShouldReturnEmptyListWhenFileDoesNotExist()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        var history = manager.LoadHistory();

        // Assert
        history.Should().BeEmpty();
    }

    [Fact]
    public void LoadHistory_ShouldLoadExistingHistory()
    {
        // Arrange
        File.WriteAllLines(_testHistoryPath, new[] { "/new test", "hello world", "/list" });
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        var history = manager.LoadHistory();

        // Assert
        history.Should().HaveCount(3);
        history[0].Should().Be("/new test");
        history[1].Should().Be("hello world");
        history[2].Should().Be("/list");
    }

    [Fact]
    public void LoadHistory_ShouldSkipEmptyLines()
    {
        // Arrange
        File.WriteAllLines(_testHistoryPath, new[] { "/new test", "", "   ", "hello" });
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act
        var history = manager.LoadHistory();

        // Assert
        history.Should().HaveCount(2);
        history[0].Should().Be("/new test");
        history[1].Should().Be("hello");
    }

    [Fact]
    public void LoadHistory_ShouldTruncateIfExceedsMaxSize()
    {
        // Arrange
        File.WriteAllLines(_testHistoryPath, new[] { "item1", "item2", "item3", "item4", "item5" });
        var manager = new ReplHistoryManager(_testHistoryPath, maxHistorySize: 3, logger: _logger);

        // Act
        var history = manager.LoadHistory();

        // Assert
        history.Should().HaveCount(3);
        history[0].Should().Be("item3");
        history[1].Should().Be("item4");
        history[2].Should().Be("item5");
    }

    [Fact]
    public void SearchHistory_ShouldReturnMatchingItems()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("/new test");
        manager.AddHistoryItem("/list");
        manager.AddHistoryItem("/new session");
        manager.AddHistoryItem("hello");

        // Act
        var results = manager.SearchHistory("new");

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().Be("/new test");
        results[1].Should().Be("/new session");
    }

    [Fact]
    public void SearchHistory_ShouldBeCaseInsensitive()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("/NEW test");
        manager.AddHistoryItem("/List");

        // Act
        var results = manager.SearchHistory("new");

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().Be("/NEW test");
    }

    [Fact]
    public void SearchHistory_ShouldReturnAllItemsWhenQueryIsEmpty()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("item1");
        manager.AddHistoryItem("item2");

        // Act
        var results = manager.SearchHistory("");

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public void ClearHistory_ShouldRemoveAllItems()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("item1");
        manager.AddHistoryItem("item2");

        // Act
        manager.ClearHistory();

        // Assert
        manager.Count.Should().Be(0);
        File.Exists(_testHistoryPath).Should().BeFalse();
    }

    [Fact]
    public void ExportHistory_ShouldWriteHistoryToFile()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("/new test");
        manager.AddHistoryItem("hello");

        var exportPath = Path.Combine(Path.GetTempPath(), $"export_{Guid.NewGuid()}.txt");

        try
        {
            // Act
            manager.ExportHistory(exportPath);

            // Assert
            File.Exists(exportPath).Should().BeTrue();
            var lines = File.ReadAllLines(exportPath);
            lines.Should().HaveCount(2);
            lines[0].Should().Be("/new test");
            lines[1].Should().Be("hello");
        }
        finally
        {
            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    [Fact]
    public void ExportHistory_ShouldThrowWhenPathIsEmpty()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.ExportHistory(""));
    }

    [Fact]
    public void ImportHistory_ShouldMergeHistoryFromFile()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("existing1");

        var importPath = Path.Combine(Path.GetTempPath(), $"import_{Guid.NewGuid()}.txt");
        File.WriteAllLines(importPath, new[] { "imported1", "imported2" });

        try
        {
            // Act
            manager.ImportHistory(importPath);

            // Assert
            manager.Count.Should().Be(3);
            var history = manager.GetAllHistory();
            history.Should().Contain("existing1");
            history.Should().Contain("imported1");
            history.Should().Contain("imported2");
        }
        finally
        {
            if (File.Exists(importPath))
            {
                File.Delete(importPath);
            }
        }
    }

    [Fact]
    public void ImportHistory_ShouldDeduplicateItems()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("duplicate");

        var importPath = Path.Combine(Path.GetTempPath(), $"import_{Guid.NewGuid()}.txt");
        File.WriteAllLines(importPath, new[] { "duplicate", "new_item" });

        try
        {
            // Act
            manager.ImportHistory(importPath);

            // Assert
            manager.Count.Should().Be(2);
            var history = manager.GetAllHistory();
            history.Where(h => h == "duplicate").Should().HaveCount(1);
        }
        finally
        {
            if (File.Exists(importPath))
            {
                File.Delete(importPath);
            }
        }
    }

    [Fact]
    public void ImportHistory_ShouldThrowWhenPathIsEmpty()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => manager.ImportHistory(""));
    }

    [Fact]
    public void ImportHistory_ShouldThrowWhenFileDoesNotExist()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => manager.ImportHistory("/nonexistent/path.txt"));
    }

    [Fact]
    public void ImportHistory_ShouldRespectMaxHistorySize()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, maxHistorySize: 3, logger: _logger);
        manager.AddHistoryItem("existing1");

        var importPath = Path.Combine(Path.GetTempPath(), $"import_{Guid.NewGuid()}.txt");
        File.WriteAllLines(importPath, new[] { "imported1", "imported2", "imported3", "imported4" });

        try
        {
            // Act
            manager.ImportHistory(importPath);

            // Assert
            manager.Count.Should().Be(3);
        }
        finally
        {
            if (File.Exists(importPath))
            {
                File.Delete(importPath);
            }
        }
    }

    [Fact]
    public void GetAllHistory_ShouldReturnReadOnlyList()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        manager.AddHistoryItem("item1");

        // Act
        var history = manager.GetAllHistory();

        // Assert
        history.Should().BeOfType<List<string>>();
        history.Should().NotBeSameAs(manager.GetAllHistory()); // 应该返回副本
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var manager = new ReplHistoryManager(_testHistoryPath, logger: _logger);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => manager.AddHistoryItem($"item{index}")));
        }

        await Task.WhenAll(tasks.ToArray());

        // Assert
        manager.Count.Should().Be(10);
    }
}
