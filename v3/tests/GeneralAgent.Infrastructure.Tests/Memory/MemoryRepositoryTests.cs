using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory;
using GeneralAgent.Infrastructure.Memory.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace GeneralAgent.Infrastructure.Tests.Memory;

/// <summary>
/// MemoryRepository 单元测试
/// 测试目标：80%+ 代码覆盖率
/// </summary>
public class MemoryRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IMemoryRepository _repository;
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MemoryOptions _options;

    public MemoryRepositoryTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _loggerFactory = NullLoggerFactory.Instance;

        // 创建临时测试目录
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"memory_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _options = new MemoryOptions
        {
            RootDirectory = _tempDirectory
        };

        _repository = new MemoryRepository(
            Options.Create(_options),
            _loggerFactory.CreateLogger<MemoryRepository>());
    }

    public void Dispose()
    {
        // 清理临时目录
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_ShouldCreateNewMemory()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "test_memory",
            "测试记忆",
            "这是测试内容"
        );

        // Act
        var saved = await _repository.SaveAsync(memory);

        // Assert
        saved.Should().NotBeNull();
        saved.Id.Should().Be(memory.Id);
        saved.Name.Should().Be("test_memory");
        saved.Type.Should().Be(MemoryType.User);

        // 验证文件已创建
        var filePath = Path.Combine(_tempDirectory, "user", "test_memory.md");
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateExistingMemory()
    {
        // Arrange - 先创建一个记忆
        var original = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "update_test",
            "原始描述",
            "原始内容"
        );
        await _repository.SaveAsync(original);

        // 更新记忆
        var updated = original with
        {
            Description = "更新后的描述",
            Content = "更新后的内容"
        };

        // Act
        var saved = await _repository.SaveAsync(updated);

        // Assert
        saved.Description.Should().Be("更新后的描述");
        saved.Content.Should().Be("更新后的内容");

        // 验证只有一个文件
        var directory = Path.Combine(_tempDirectory, "feedback");
        Directory.GetFiles(directory).Length.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_WithTags_ShouldPersistTags()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Project,
            "tagged_memory",
            "带标签的记忆",
            "内容",
            new List<string> { "重要", "紧急", "项目A" }
        );

        // Act
        var saved = await _repository.SaveAsync(memory);

        // Assert
        saved.Tags.Should().HaveCount(3);
        saved.Tags.Should().Contain("重要");
        saved.Tags.Should().Contain("紧急");
        saved.Tags.Should().Contain("项目A");

        // 重新读取验证
        var retrieved = await _repository.GetByIdAsync(saved.Id);
        retrieved!.Tags.Should().BeEquivalentTo(memory.Tags);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnMemory()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "get_by_id_test",
            "测试",
            "内容"
        );
        await _repository.SaveAsync(memory);

        // Act
        var retrieved = await _repository.GetByIdAsync(memory.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(memory.Id);
        retrieved.Name.Should().Be(memory.Name);
        retrieved.Content.Should().Be(memory.Content);
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

    #endregion

    #region GetByIdsAsync Tests

    [Fact]
    public async Task GetByIdsAsync_WithValidIds_ShouldReturnAllMatchingMemories()
    {
        // Arrange
        var memory1 = Core.Models.Memory.Create(MemoryType.User, "m1", "d1", "c1");
        var memory2 = Core.Models.Memory.Create(MemoryType.User, "m2", "d2", "c2");
        var memory3 = Core.Models.Memory.Create(MemoryType.Feedback, "m3", "d3", "c3");

        await _repository.SaveAsync(memory1);
        await _repository.SaveAsync(memory2);
        await _repository.SaveAsync(memory3);

        var ids = new[] { memory1.Id, memory3.Id };

        // Act
        var result = await _repository.GetByIdsAsync(ids);

        // Assert
        result.Should().HaveCount(2);
        result.Select(m => m.Id).Should().BeEquivalentTo(new[] { memory1.Id, memory3.Id });
        result.Select(m => m.Name).Should().BeEquivalentTo(new[] { "m1", "m3" });
    }

    [Fact]
    public async Task GetByIdsAsync_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c"));

        // Act
        var result = await _repository.GetByIdsAsync(Array.Empty<Guid>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_WithNonExistentIds_ShouldReturnOnlyExisting()
    {
        // Arrange
        var memory1 = Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c");
        await _repository.SaveAsync(memory1);

        var nonExistentId = Guid.NewGuid();
        var ids = new[] { memory1.Id, nonExistentId };

        // Act
        var result = await _repository.GetByIdsAsync(ids);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(memory1.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_WithDuplicateIds_ShouldReturnUniqueMemories()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(MemoryType.Project, "m1", "d", "c");
        await _repository.SaveAsync(memory);

        var ids = new[] { memory.Id, memory.Id, memory.Id }; // Duplicate IDs

        // Act
        var result = await _repository.GetByIdsAsync(ids);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(memory.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_WithAllNonExistentIds_ShouldReturnEmptyList()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c"));

        var nonExistentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var result = await _repository.GetByIdsAsync(nonExistentIds);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByNameAsync Tests

    [Fact]
    public async Task GetByNameAsync_WhenExists_ShouldReturnMemory()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Reference,
            "unique_name",
            "测试",
            "内容"
        );
        await _repository.SaveAsync(memory);

        // Act
        var retrieved = await _repository.GetByNameAsync("unique_name", MemoryType.Reference);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("unique_name");
        retrieved.Type.Should().Be(MemoryType.Reference);
    }

    [Fact]
    public async Task GetByNameAsync_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByNameAsync("non_existent", MemoryType.User);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_DifferentTypes_ShouldBeIndependent()
    {
        // Arrange - 创建两个同名但不同类型的记忆
        var userMemory = Core.Models.Memory.Create(
            MemoryType.User,
            "same_name",
            "用户记忆",
            "用户内容"
        );
        var projectMemory = Core.Models.Memory.Create(
            MemoryType.Project,
            "same_name",
            "项目记忆",
            "项目内容"
        );

        await _repository.SaveAsync(userMemory);
        await _repository.SaveAsync(projectMemory);

        // Act
        var user = await _repository.GetByNameAsync("same_name", MemoryType.User);
        var project = await _repository.GetByNameAsync("same_name", MemoryType.Project);

        // Assert
        user.Should().NotBeNull();
        project.Should().NotBeNull();
        user!.Content.Should().Be("用户内容");
        project!.Content.Should().Be("项目内容");
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ShouldReturnEmptyList()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllMemories()
    {
        // Arrange - 创建多个不同类型的记忆
        var memories = new[]
        {
            Core.Models.Memory.Create(MemoryType.User, "memory1", "desc1", "content1"),
            Core.Models.Memory.Create(MemoryType.Feedback, "memory2", "desc2", "content2"),
            Core.Models.Memory.Create(MemoryType.Project, "memory3", "desc3", "content3"),
        };

        foreach (var memory in memories)
        {
            await _repository.SaveAsync(memory);
        }

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(m => m.Name).Should().BeEquivalentTo(new[] { "memory1", "memory2", "memory3" });
    }

    #endregion

    #region GetByTypeAsync Tests

    [Fact]
    public async Task GetByTypeAsync_ShouldReturnOnlyMatchingType()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "user1", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "user2", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.Project, "proj1", "d", "c"));

        // Act
        var userMemories = await _repository.GetByTypeAsync(MemoryType.User);
        var projectMemories = await _repository.GetByTypeAsync(MemoryType.Project);

        // Assert
        userMemories.Should().HaveCount(2);
        userMemories.All(m => m.Type == MemoryType.User).Should().BeTrue();

        projectMemories.Should().HaveCount(1);
        projectMemories[0].Name.Should().Be("proj1");
    }

    [Fact]
    public async Task GetByTypeAsync_WhenNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "user1", "d", "c"));

        // Act
        var result = await _repository.GetByTypeAsync(MemoryType.Knowledge);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ByName_ShouldFindMemory()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "coding_style",
            "编码风格偏好",
            "喜欢函数式编程"
        ));

        // Act
        var result = await _repository.SearchAsync("coding");

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("coding_style");
    }

    [Fact]
    public async Task SearchAsync_ByDescription_ShouldFindMemory()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "pref1",
            "喜欢使用 TDD 开发",
            "内容"
        ));

        // Act
        var result = await _repository.SearchAsync("TDD");

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_ByContent_ShouldFindMemory()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Project,
            "note1",
            "描述",
            "这个项目使用 ASP.NET Core 开发"
        ));

        // Act
        var result = await _repository.SearchAsync("ASP.NET");

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_ByTags_ShouldFindMemory()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Reference,
            "tagged",
            "描述",
            "内容",
            new List<string> { "重要", "C#" }
        ));

        // Act
        var result = await _repository.SearchAsync("重要");

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_WithTypeFilter_ShouldReturnOnlyMatchingType()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "user_test",
            "测试",
            "内容"
        ));
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Project,
            "project_test",
            "测试",
            "内容"
        ));

        // Act
        var result = await _repository.SearchAsync("测试", MemoryType.User);

        // Assert
        result.Should().HaveCount(1);
        result[0].Type.Should().Be(MemoryType.User);
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_ShouldFindMemory()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "test",
            "Test Description",
            "Test Content"
        ));

        // Act
        var result = await _repository.SearchAsync("test");

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SearchAsync_WhenNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c"));

        // Act
        var result = await _repository.SearchAsync("不存在的关键词");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region SearchByTagsAsync Tests

    [Fact]
    public async Task SearchByTagsAsync_SingleTag_ShouldFindMemories()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "m1",
            "d",
            "c",
            new List<string> { "重要" }
        ));
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Project,
            "m2",
            "d",
            "c",
            new List<string> { "重要", "紧急" }
        ));

        // Act
        var result = await _repository.SearchByTagsAsync(new List<string> { "重要" });

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchByTagsAsync_MultipleTags_ShouldFindMemoriesWithAnyTag()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "m1",
            "d",
            "c",
            new List<string> { "C#" }
        ));
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "m2",
            "d",
            "c",
            new List<string> { "Python" }
        ));
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "m3",
            "d",
            "c",
            new List<string> { "Java" }
        ));

        // Act
        var result = await _repository.SearchByTagsAsync(new List<string> { "C#", "Python" });

        // Assert
        result.Should().HaveCount(2);
        result.Select(m => m.Name).Should().BeEquivalentTo(new[] { "m1", "m2" });
    }

    [Fact]
    public async Task SearchByTagsAsync_WhenNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "m1",
            "d",
            "c",
            new List<string> { "标签1" }
        ));

        // Act
        var result = await _repository.SearchByTagsAsync(new List<string> { "不存在的标签" });

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingMemory()
    {
        // Arrange
        var original = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "update_test",
            "原始描述",
            "原始内容"
        );
        await _repository.SaveAsync(original);

        var updated = original with
        {
            Description = "新描述",
            Content = "新内容",
            Tags = new List<string> { "新标签" }
        };

        // Act
        var result = await _repository.UpdateAsync(updated);

        // Assert
        result.Description.Should().Be("新描述");
        result.Content.Should().Be("新内容");
        result.Tags.Should().Contain("新标签");

        // 验证持久化
        var retrieved = await _repository.GetByIdAsync(original.Id);
        retrieved!.Description.Should().Be("新描述");
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveId()
    {
        // Arrange
        var original = Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c");
        await _repository.SaveAsync(original);

        var updated = original with { Content = "新内容" };

        // Act
        var result = await _repository.UpdateAsync(updated);

        // Assert
        result.Id.Should().Be(original.Id);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldRemoveMemory()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(MemoryType.User, "to_delete", "d", "c");
        await _repository.SaveAsync(memory);

        // Act
        var result = await _repository.DeleteAsync(memory.Id);

        // Assert
        result.Should().BeTrue();

        // 验证文件已删除
        var retrieved = await _repository.GetByIdAsync(memory.Id);
        retrieved.Should().BeNull();

        var filePath = Path.Combine(_tempDirectory, "user", "to_delete.md");
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.DeleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_WhenExists_ShouldReturnTrue()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(MemoryType.User, "exists_test", "d", "c");
        await _repository.SaveAsync(memory);

        // Act
        var result = await _repository.ExistsAsync(memory.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.ExistsAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region NameExistsAsync Tests

    [Fact]
    public async Task NameExistsAsync_WhenExists_ShouldReturnTrue()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "unique_name",
            "d",
            "c"
        ));

        // Act
        var result = await _repository.NameExistsAsync("unique_name", MemoryType.User);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_WhenNotExists_ShouldReturnFalse()
    {
        // Act
        var result = await _repository.NameExistsAsync("non_existent", MemoryType.User);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_DifferentTypes_ShouldBeIndependent()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "same_name",
            "d",
            "c"
        ));

        // Act
        var existsAsUser = await _repository.NameExistsAsync("same_name", MemoryType.User);
        var existsAsProject = await _repository.NameExistsAsync("same_name", MemoryType.Project);

        // Assert
        existsAsUser.Should().BeTrue();
        existsAsProject.Should().BeFalse();
    }

    #endregion

    #region File Format Tests

    [Fact]
    public async Task FileFormat_ShouldContainFrontmatter()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "format_test",
            "测试描述",
            "测试内容",
            new List<string> { "标签1", "标签2" }
        );
        await _repository.SaveAsync(memory);

        // Act
        var filePath = Path.Combine(_tempDirectory, "user", "format_test.md");
        var content = await File.ReadAllTextAsync(filePath);

        // Assert
        content.Should().Contain("---");
        content.Should().Contain("name: format_test");
        content.Should().Contain("type: User");
        content.Should().Contain("description: 测试描述");
        content.Should().Contain("tags: 标签1, 标签2"); // 标签是逗号分隔的
        content.Should().Contain("测试内容");
    }

    [Fact]
    public async Task FileFormat_ShouldBeReadableAfterSave()
    {
        // Arrange
        var original = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "read_test",
            "描述",
            "内容包含多行\n第二行\n第三行",
            new List<string> { "测试" }
        );
        await _repository.SaveAsync(original);

        // Act
        var retrieved = await _repository.GetByIdAsync(original.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be(original.Name);
        retrieved.Type.Should().Be(original.Type);
        retrieved.Description.Should().Be(original.Description);
        retrieved.Content.Should().Be(original.Content);
        retrieved.Tags.Should().BeEquivalentTo(original.Tags);
    }

    #endregion

    #region Performance Tests (N+1 Query Optimization)

    [Fact]
    public async Task GetByIdsAsync_WithIndexOptimization_ShouldBeFasterThanBeforeOptimization()
    {
        // Arrange: 创建 100 个记忆
        var memories = new List<Core.Models.Memory>();
        for (int i = 0; i < 100; i++)
        {
            var memory = Core.Models.Memory.Create(
                MemoryType.User,
                $"perf_test_{i}",
                $"Description {i}",
                $"Content {i}");
            await _repository.SaveAsync(memory);
            memories.Add(memory);
        }

        // 选择 10 个 ID 进行批量查询
        var ids = memories.Take(10).Select(m => m.Id).ToArray();

        // Act: 测量批量查询性能
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.GetByIdsAsync(ids);
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(10);

        // 验证性能：批量查询 10 个记忆应该在 100ms 内完成（优化前可能需要 500ms+）
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
            $"GetByIdsAsync should be fast with index optimization. Actual: {stopwatch.ElapsedMilliseconds}ms");

        _testOutputHelper.WriteLine($"✅ GetByIdsAsync (10 items) completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetByIdAsync_WithIndexOptimization_ShouldNotLoadAllMemories()
    {
        // Arrange: 创建 50 个记忆
        var targetMemory = Core.Models.Memory.Create(MemoryType.User, "target", "d", "c");
        await _repository.SaveAsync(targetMemory);

        for (int i = 0; i < 49; i++)
        {
            await _repository.SaveAsync(
                Core.Models.Memory.Create(MemoryType.User, $"other_{i}", "d", "c"));
        }

        // Act: 测量单个查询性能
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository.GetByIdAsync(targetMemory.Id);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(targetMemory.Id);

        // 验证性能：单个查询应该在 50ms 内完成（优化前需要加载所有 50 个文件）
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
            $"GetByIdAsync should be fast with index optimization. Actual: {stopwatch.ElapsedMilliseconds}ms");

        _testOutputHelper.WriteLine($"✅ GetByIdAsync completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task IndexBuilding_ShouldBeEfficient()
    {
        // Arrange: 创建 100 个记忆
        for (int i = 0; i < 100; i++)
        {
            await _repository.SaveAsync(
                Core.Models.Memory.Create(MemoryType.User, $"index_test_{i}", "d", "c"));
        }

        // 创建新的 repository 实例（强制重建索引）
        var newRepository = new MemoryRepository(
            Microsoft.Extensions.Options.Options.Create(_options),
            _loggerFactory.CreateLogger<MemoryRepository>());

        // Act: 测量索引构建性能（第一次查询时懒加载）
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await newRepository.GetByIdAsync(Guid.NewGuid()); // 触发索引构建
        stopwatch.Stop();

        // Assert: 索引构建应该在 200ms 内完成
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200,
            $"Index building for 100 memories should be fast. Actual: {stopwatch.ElapsedMilliseconds}ms");

        _testOutputHelper.WriteLine($"✅ Index building (100 memories) completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion
}
