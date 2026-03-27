using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory;
using GeneralAgent.Infrastructure.Memory.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.Tests.Memory;

/// <summary>
/// MemoryIndexManager 单元测试
/// 测试目标：80%+ 代码覆盖率
/// 注意：IndexManager 的方法都基于 Repository 中的实际文件，索引文件只是一个视图
/// </summary>
public class MemoryIndexManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IMemoryRepository _repository;
    private readonly IMemoryIndexManager _indexManager;

    public MemoryIndexManagerTests()
    {
        // 创建临时测试目录
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"memory_index_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        var options = Options.Create(new MemoryOptions
        {
            RootDirectory = _tempDirectory
        });

        // 先创建 Repository，再创建 IndexManager（IndexManager 依赖 Repository）
        _repository = new MemoryRepository(options, NullLogger<MemoryRepository>.Instance);
        _indexManager = new MemoryIndexManager(options, _repository, NullLogger<MemoryIndexManager>.Instance);
    }

    public void Dispose()
    {
        // 清理临时目录
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region RebuildIndexAsync Tests

    [Fact]
    public async Task RebuildIndexAsync_WhenEmpty_ShouldCreateEmptyIndex()
    {
        // Act
        await _indexManager.RebuildIndexAsync();

        // Assert
        var indexPath = Path.Combine(_tempDirectory, "MEMORY.md");
        File.Exists(indexPath).Should().BeTrue();

        var content = await File.ReadAllTextAsync(indexPath);
        content.Should().Match(c => c.Contains("# Memory Index") || c.Contains("# CoreMemory Index"));
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldIncludeAllMemories()
    {
        // Arrange - 创建多个记忆（注意：需要先保存到 Repository）
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.User,
            "user_memory",
            "用户记忆",
            "内容"
        ));
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Project,
            "project_memory",
            "项目记忆",
            "内容"
        ));
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Feedback,
            "feedback_memory",
            "反馈记忆",
            "内容"
        ));

        // Act
        await _indexManager.RebuildIndexAsync();

        // Assert
        var indexPath = Path.Combine(_tempDirectory, "MEMORY.md");
        var content = await File.ReadAllTextAsync(indexPath);

        content.Should().Contain("user_memory");
        content.Should().Contain("project_memory");
        content.Should().Contain("feedback_memory");
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldGroupByType()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "u1", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "u2", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.Project, "p1", "d", "c"));

        // Act
        await _indexManager.RebuildIndexAsync();

        // Assert
        var indexPath = Path.Combine(_tempDirectory, "MEMORY.md");
        var content = await File.ReadAllTextAsync(indexPath);

        // 验证分组结构
        content.Should().Contain("## User");
        content.Should().Contain("## Project");

        // User 类型的记忆应该在一起
        var userSection = content.IndexOf("## User");
        var projectSection = content.IndexOf("## Project");
        var u1Index = content.IndexOf("u1");
        var u2Index = content.IndexOf("u2");
        var p1Index = content.IndexOf("p1");

        u1Index.Should().BeGreaterThan(userSection);
        u2Index.Should().BeGreaterThan(userSection);
        p1Index.Should().BeGreaterThan(projectSection);
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldIncludeDescription()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(
            MemoryType.Reference,
            "test_memory",
            "这是一个测试描述",
            "内容"
        ));

        // Act
        await _indexManager.RebuildIndexAsync();

        // Assert
        var indexPath = Path.Combine(_tempDirectory, "MEMORY.md");
        var content = await File.ReadAllTextAsync(indexPath);

        content.Should().Contain("这是一个测试描述");
    }

    #endregion

    #region AddToIndexAsync Tests

    [Fact]
    public async Task AddToIndexAsync_AfterSavingMemory_ShouldReflectInIndex()
    {
        // Arrange - 先保存记忆到 Repository
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "new_memory",
            "新记忆",
            "内容"
        );
        await _repository.SaveAsync(memory);

        // Act - 触发索引更新
        await _indexManager.AddToIndexAsync(memory);

        // Assert - GetAllIndexEntriesAsync 应该能读取到记忆
        var entries = await _indexManager.GetAllIndexEntriesAsync();
        entries.Should().HaveCount(1);
        entries[0].Name.Should().Be("new_memory");
        entries[0].Description.Should().Be("新记忆");
    }

    [Fact]
    public async Task AddToIndexAsync_MultipleTimes_ShouldMaintainConsistency()
    {
        // Arrange
        var memory1 = Core.Models.Memory.Create(MemoryType.User, "m1", "d1", "c");
        var memory2 = Core.Models.Memory.Create(MemoryType.Feedback, "m2", "d2", "c");

        await _repository.SaveAsync(memory1);
        await _repository.SaveAsync(memory2);

        // Act
        await _indexManager.AddToIndexAsync(memory1);
        await _indexManager.AddToIndexAsync(memory2);

        // Assert
        var entries = await _indexManager.GetAllIndexEntriesAsync();
        entries.Should().HaveCount(2);
    }

    #endregion

    #region RemoveFromIndexAsync Tests

    [Fact]
    public async Task RemoveFromIndexAsync_AfterDeletingMemory_ShouldReflectInIndex()
    {
        // Arrange - 先保存再删除
        var memory = Core.Models.Memory.Create(MemoryType.User, "to_remove", "d", "c");
        await _repository.SaveAsync(memory);
        await _indexManager.AddToIndexAsync(memory);

        // 删除记忆
        await _repository.DeleteAsync(memory.Id);

        // Act - 触发索引更新
        await _indexManager.RemoveFromIndexAsync(memory.Id);

        // Assert
        var entries = await _indexManager.GetAllIndexEntriesAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveFromIndexAsync_WhenNotExists_ShouldNotThrow()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var act = async () => await _indexManager.RemoveFromIndexAsync(nonExistentId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveFromIndexAsync_ShouldKeepOtherMemories()
    {
        // Arrange
        var memory1 = Core.Models.Memory.Create(MemoryType.User, "m1", "d1", "c");
        var memory2 = Core.Models.Memory.Create(MemoryType.User, "m2", "d2", "c");
        var memory3 = Core.Models.Memory.Create(MemoryType.User, "m3", "d3", "c");

        await _repository.SaveAsync(memory1);
        await _repository.SaveAsync(memory2);
        await _repository.SaveAsync(memory3);

        // Act - 删除中间的记忆
        await _repository.DeleteAsync(memory2.Id);
        await _indexManager.RemoveFromIndexAsync(memory2.Id);

        // Assert
        var entries = await _indexManager.GetAllIndexEntriesAsync();
        entries.Should().HaveCount(2);
        entries.Select(e => e.Name).Should().BeEquivalentTo(new[] { "m1", "m3" });
    }

    #endregion

    #region UpdateInIndexAsync Tests

    [Fact]
    public async Task UpdateInIndexAsync_AfterUpdatingMemory_ShouldReflectInIndex()
    {
        // Arrange
        var original = Core.Models.Memory.Create(MemoryType.User, "update_test", "原描述", "c");
        await _repository.SaveAsync(original);

        var updated = original with { Description = "新描述" };
        await _repository.UpdateAsync(updated);

        // Act
        await _indexManager.UpdateInIndexAsync(updated);

        // Assert
        var entries = await _indexManager.GetAllIndexEntriesAsync();
        entries.Should().HaveCount(1);
        entries[0].Description.Should().Be("新描述");
    }

    #endregion

    #region GetAllIndexEntriesAsync Tests

    [Fact]
    public async Task GetAllIndexEntriesAsync_WhenEmpty_ShouldReturnEmptyList()
    {
        // Act
        var result = await _indexManager.GetAllIndexEntriesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllIndexEntriesAsync_ShouldReturnAllMemories()
    {
        // Arrange
        var memories = new[]
        {
            Core.Models.Memory.Create(MemoryType.User, "m1", "d1", "c"),
            Core.Models.Memory.Create(MemoryType.Feedback, "m2", "d2", "c"),
            Core.Models.Memory.Create(MemoryType.Project, "m3", "d3", "c"),
        };

        foreach (var memory in memories)
        {
            await _repository.SaveAsync(memory);
        }

        // Act
        var result = await _indexManager.GetAllIndexEntriesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Select(e => e.Name).Should().BeEquivalentTo(new[] { "m1", "m2", "m3" });
    }

    #endregion

    #region GetIndexEntriesByTypeAsync Tests

    [Fact]
    public async Task GetIndexEntriesByTypeAsync_ShouldReturnOnlyMatchingType()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "u1", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "u2", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.Project, "p1", "d", "c"));

        // Act
        var userEntries = await _indexManager.GetIndexEntriesByTypeAsync(MemoryType.User);
        var projectEntries = await _indexManager.GetIndexEntriesByTypeAsync(MemoryType.Project);

        // Assert
        userEntries.Should().HaveCount(2);
        userEntries.All(e => e.Type == MemoryType.User).Should().BeTrue();

        projectEntries.Should().HaveCount(1);
        projectEntries[0].Name.Should().Be("p1");
    }

    [Fact]
    public async Task GetIndexEntriesByTypeAsync_WhenNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "u1", "d", "c"));

        // Act
        var result = await _indexManager.GetIndexEntriesByTypeAsync(MemoryType.Knowledge);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ValidateIndexAsync Tests

    [Fact]
    public async Task ValidateIndexAsync_WhenEmpty_ShouldReturnTrue()
    {
        // Arrange - 先重建空索引
        await _indexManager.RebuildIndexAsync();

        // Act
        var result = await _indexManager.ValidateIndexAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateIndexAsync_WhenConsistent_ShouldReturnTrue()
    {
        // Arrange - 保存记忆并重建索引
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.Feedback, "m2", "d", "c"));

        // 重建索引以确保一致性
        await _indexManager.RebuildIndexAsync();

        // Act
        var result = await _indexManager.ValidateIndexAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateIndexAsync_WhenFilesMissingFromIndex_ShouldReturnFalse()
    {
        // Arrange - 创建一个禁用自动重建的索引管理器
        var options = Options.Create(new MemoryOptions
        {
            RootDirectory = _tempDirectory,
            AutoRebuildCorruptedIndex = false  // 禁用自动重建以测试验证功能
        });
        var testIndexManager = new MemoryIndexManager(options, _repository, NullLogger<MemoryIndexManager>.Instance);

        // 先建立一致的索引
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c"));
        await testIndexManager.RebuildIndexAsync();

        // 然后直接创建新文件（不更新索引）
        var userDir = Path.Combine(_tempDirectory, "user");
        var memory = Core.Models.Memory.Create(MemoryType.User, "orphan", "描述", "内容");
        var filePath = Path.Combine(userDir, "orphan.md");

        var yamlContent = $@"---
id: {memory.Id}
name: orphan
type: User
description: 描述
tags:
created_at: {memory.CreatedAt:O}
updated_at: {memory.UpdatedAt:O}
---

内容";

        await File.WriteAllTextAsync(filePath, yamlContent);

        // Act
        var result = await testIndexManager.ValidateIndexAsync();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_FullLifecycle_ShouldMaintainIndexConsistency()
    {
        // Arrange & Act
        // 1. 创建记忆
        var memory1 = await _repository.SaveAsync(
            Core.Models.Memory.Create(MemoryType.User, "lifecycle_test", "描述", "内容")
        );
        await _indexManager.AddToIndexAsync(memory1);

        // 验证索引
        var entries1 = await _indexManager.GetAllIndexEntriesAsync();
        entries1.Should().HaveCount(1);

        // 2. 更新记忆
        var updated = memory1 with { Description = "新描述" };
        await _repository.UpdateAsync(updated);
        await _indexManager.UpdateInIndexAsync(updated);

        // 验证索引更新
        var entries2 = await _indexManager.GetAllIndexEntriesAsync();
        entries2.Should().HaveCount(1);
        entries2[0].Description.Should().Be("新描述");

        // 3. 删除记忆
        await _repository.DeleteAsync(memory1.Id);
        await _indexManager.RemoveFromIndexAsync(memory1.Id);

        // 验证索引清空
        var entries3 = await _indexManager.GetAllIndexEntriesAsync();
        entries3.Should().BeEmpty();

        // Assert - 重建索引后应该仍然一致
        await _indexManager.RebuildIndexAsync();
        var isValid = await _indexManager.ValidateIndexAsync();
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task Integration_RebuildIndex_ShouldRegenerateIndexFile()
    {
        // Arrange - 保存一些记忆
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d1", "c"));
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.Feedback, "m2", "d2", "c"));
        await _indexManager.RebuildIndexAsync();

        // 2. 手动修改索引文件添加不存在的记忆
        var indexPath = Path.Combine(_tempDirectory, "MEMORY.md");
        var originalContent = await File.ReadAllTextAsync(indexPath);
        var modifiedContent = originalContent + "\n- [ghost](project/ghost.md) — 幽灵记忆\n";
        await File.WriteAllTextAsync(indexPath, modifiedContent);

        // 验证 ghost 被添加到索引
        var contentBefore = await File.ReadAllTextAsync(indexPath);
        contentBefore.Should().Contain("ghost");

        // Act - 重建索引应该移除 ghost
        await _indexManager.RebuildIndexAsync();

        // Assert - ghost 应该被移除，只保留实际的记忆
        var contentAfter = await File.ReadAllTextAsync(indexPath);
        contentAfter.Should().NotContain("ghost");
        contentAfter.Should().Contain("m1");
        contentAfter.Should().Contain("m2");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EdgeCase_IndexFileDoesNotExist_ValidateShouldReturnFalse()
    {
        // Arrange - 确保索引文件不存在
        var indexPath = Path.Combine(_tempDirectory, "MEMORY.md");
        if (File.Exists(indexPath))
        {
            File.Delete(indexPath);
        }

        // Act
        var result = await _indexManager.ValidateIndexAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EdgeCase_GetAllIndexEntries_ShouldAlwaysReflectRepositoryState()
    {
        // Arrange - 保存记忆但不更新索引
        await _repository.SaveAsync(Core.Models.Memory.Create(MemoryType.User, "m1", "d", "c"));

        // Act - GetAllIndexEntriesAsync 应该直接从 Repository 读取
        var entries = await _indexManager.GetAllIndexEntriesAsync();

        // Assert - 即使没有调用 AddToIndexAsync，也应该能读取到记忆
        entries.Should().HaveCount(1);
    }

    #endregion
}
