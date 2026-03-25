using GeneralAgent.Hosts.Console.Repl;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeneralAgent.Hosts.Console.Tests.Repl;

/// <summary>
/// AliasManager 测试
/// </summary>
public class AliasManagerTests : IDisposable
{
    private readonly string _tempFilePath;
    private readonly AliasManager _manager;

    public AliasManagerTests()
    {
        // 创建临时文件路径
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test_aliases_{Guid.NewGuid()}.json");
        _manager = new AliasManager(_tempFilePath, NullLogger<AliasManager>.Instance);
    }

    public void Dispose()
    {
        // 清理临时文件
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Fact]
    public void Constructor_ShouldCreateDefaultAliases()
    {
        // Act
        var aliases = _manager.GetAllAliases();

        // Assert
        Assert.True(aliases.Count > 0, "应该创建默认别名");
        Assert.True(aliases.ContainsKey("n"), "应该包含 'n' 别名");
        Assert.Equal("new", aliases["n"]);
    }

    [Fact]
    public void AddAlias_ShouldAddNewAlias()
    {
        // Act
        _manager.AddAlias("test", "testing");
        var aliases = _manager.GetAllAliases();

        // Assert
        Assert.True(aliases.ContainsKey("test"), "应该添加新别名");
        Assert.Equal("testing", aliases["test"]);
    }

    [Fact]
    public void AddAlias_WithEmptyAlias_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _manager.AddAlias("", "command"));
        Assert.Throws<ArgumentException>(() => _manager.AddAlias("  ", "command"));
    }

    [Fact]
    public void AddAlias_WithEmptyCommand_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _manager.AddAlias("alias", ""));
        Assert.Throws<ArgumentException>(() => _manager.AddAlias("alias", "  "));
    }

    [Fact]
    public void RemoveAlias_ExistingAlias_ShouldReturnTrue()
    {
        // Arrange
        _manager.AddAlias("temp", "temporary");

        // Act
        var result = _manager.RemoveAlias("temp");
        var aliases = _manager.GetAllAliases();

        // Assert
        Assert.True(result, "应该返回 true");
        Assert.False(aliases.ContainsKey("temp"), "别名应该被移除");
    }

    [Fact]
    public void RemoveAlias_NonExistingAlias_ShouldReturnFalse()
    {
        // Act
        var result = _manager.RemoveAlias("nonexistent");

        // Assert
        Assert.False(result, "应该返回 false");
    }

    [Fact]
    public void ResolveAlias_SimpleAlias_ShouldResolve()
    {
        // Arrange
        _manager.AddAlias("n", "new");

        // Act
        var resolved = _manager.ResolveAlias("/n");

        // Assert
        Assert.Equal("/new", resolved);
    }

    [Fact]
    public void ResolveAlias_WithArguments_ShouldPreserveArguments()
    {
        // Arrange
        _manager.AddAlias("n", "new");

        // Act
        var resolved = _manager.ResolveAlias("/n 测试会话");

        // Assert
        Assert.Equal("/new 测试会话", resolved);
    }

    [Fact]
    public void ResolveAlias_ChainedAliases_ShouldResolveRecursively()
    {
        // Arrange
        _manager.AddAlias("a", "b");
        _manager.AddAlias("b", "c");
        _manager.AddAlias("c", "final");

        // Act
        var resolved = _manager.ResolveAlias("/a");

        // Assert
        Assert.Equal("/final", resolved);
    }

    [Fact]
    public void ResolveAlias_NonCommandInput_ShouldReturnOriginal()
    {
        // Act
        var resolved = _manager.ResolveAlias("hello world");

        // Assert
        Assert.Equal("hello world", resolved);
    }

    [Fact]
    public void ResolveAlias_UnknownCommand_ShouldReturnOriginal()
    {
        // Act
        var resolved = _manager.ResolveAlias("/unknown");

        // Assert
        Assert.Equal("/unknown", resolved);
    }

    [Fact]
    public void AddAlias_CircularReference_ShouldThrowException()
    {
        // Arrange
        _manager.AddAlias("a", "b");
        _manager.AddAlias("b", "c");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _manager.AddAlias("c", "a"));
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistAliases()
    {
        // Arrange
        _manager.AddAlias("custom", "command");
        _manager.SaveAliases();

        // Act - 创建新的 manager 实例加载配置
        var newManager = new AliasManager(_tempFilePath, NullLogger<AliasManager>.Instance);
        var aliases = newManager.GetAllAliases();

        // Assert
        Assert.True(aliases.ContainsKey("custom"), "应该加载自定义别名");
        Assert.Equal("command", aliases["custom"]);
    }

    [Fact]
    public void HasAlias_ExistingAlias_ShouldReturnTrue()
    {
        // Arrange
        _manager.AddAlias("test", "testing");

        // Act
        var result = _manager.HasAlias("test");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAlias_NonExistingAlias_ShouldReturnFalse()
    {
        // Act
        var result = _manager.HasAlias("nonexistent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetAllAliases_ShouldReturnReadOnlyCopy()
    {
        // Act
        var aliases1 = _manager.GetAllAliases();
        _manager.AddAlias("new", "alias");
        var aliases2 = _manager.GetAllAliases();

        // Assert
        Assert.NotEqual(aliases1.Count, aliases2.Count);
        Assert.False(aliases1.ContainsKey("new"), "第一个副本不应该受到修改影响");
        Assert.True(aliases2.ContainsKey("new"), "第二个副本应该包含新别名");
    }
}
