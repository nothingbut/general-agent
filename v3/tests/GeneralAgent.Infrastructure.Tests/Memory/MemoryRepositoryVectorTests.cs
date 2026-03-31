using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Memory;
using GeneralAgent.Infrastructure.Memory.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GeneralAgent.Infrastructure.Tests.Memory;

/// <summary>
/// MemoryRepository 向量数据库集成测试
/// 测试目标：验证双写逻辑、容错行为、正确的参数传递
/// </summary>
public class MemoryRepositoryVectorTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly IVectorRepository _mockVectorRepository;
    private readonly IEmbeddingClient _mockEmbeddingClient;
    private readonly IMemoryRepository _repository;

    public MemoryRepositoryVectorTests()
    {
        // 创建临时测试目录
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"memory_vector_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // 创建 mock 依赖
        _mockVectorRepository = Substitute.For<IVectorRepository>();
        _mockEmbeddingClient = Substitute.For<IEmbeddingClient>();

        // 配置 mock 的默认返回值
        _mockEmbeddingClient.Dimensions.Returns(768);
        _mockEmbeddingClient.ProviderName.Returns("MockProvider");

        var options = Options.Create(new MemoryOptions
        {
            RootDirectory = _tempDirectory
        });

        _repository = new MemoryRepository(
            options,
            NullLogger<MemoryRepository>.Instance,
            _mockVectorRepository,
            _mockEmbeddingClient);
    }

    public void Dispose()
    {
        // 清理临时目录
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region SaveAsync 双写测试

    [Fact]
    public async Task SaveAsync_ShouldCallEmbeddingClient_WithCorrectText()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "test_memory",
            "测试描述",
            "测试内容"
        );

        var mockEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        // Act
        await _repository.SaveAsync(memory);

        // Assert
        await _mockEmbeddingClient.Received(1).GenerateEmbeddingAsync(
            Arg.Is<string>(text => text == "test_memory 测试描述 测试内容"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldCallVectorRepository_WithCorrectParameters()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "test_memory",
            "测试描述",
            "测试内容",
            new List<string> { "标签1", "标签2" }
        );

        var mockEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        // Act
        await _repository.SaveAsync(memory);

        // Assert
        await _mockVectorRepository.Received(1).UpsertAsync(
            Arg.Is<Guid>(id => id == memory.Id),
            Arg.Is<float[]>(embedding => embedding == mockEmbedding),
            Arg.Is<Dictionary<string, object>>(metadata =>
                metadata.ContainsKey("memory_id") &&
                metadata["memory_id"].ToString() == memory.Id.ToString() &&
                metadata["type"].ToString() == "User" &&
                metadata["name"].ToString() == "test_memory" &&
                metadata["description"].ToString() == "测试描述" &&
                metadata["tags"].ToString() == "标签1,标签2" &&
                metadata.ContainsKey("created_at") &&
                metadata.ContainsKey("updated_at")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldIncludeMemoryIdInMetadata()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Project,
            "project_memory",
            "描述",
            "内容"
        );

        var mockEmbedding = new float[] { 0.1f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        Dictionary<string, object>? capturedMetadata = null;
        await _mockVectorRepository.UpsertAsync(
            Arg.Any<Guid>(),
            Arg.Any<float[]>(),
            Arg.Do<Dictionary<string, object>>(m => capturedMetadata = m),
            Arg.Any<CancellationToken>());

        // Act
        await _repository.SaveAsync(memory);

        // Assert
        capturedMetadata.Should().NotBeNull();
        capturedMetadata!.Should().ContainKey("memory_id");
        capturedMetadata!.TryGetValue("memory_id", out var memoryIdValue).Should().BeTrue();
        memoryIdValue.Should().NotBeNull();
        memoryIdValue!.ToString().Should().Be(memory.Id.ToString());
    }

    [Fact]
    public async Task SaveAsync_WhenEmbeddingFails_ShouldStillSaveToFileSystem()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "error_test",
            "描述",
            "内容"
        );

        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("Embedding 生成失败"));

        // Act
        var result = await _repository.SaveAsync(memory);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(memory.Id);

        // 验证文件已保存
        var filePath = Path.Combine(_tempDirectory, "user", "error_test.md");
        File.Exists(filePath).Should().BeTrue();

        // 验证 VectorRepository 未被调用
        await _mockVectorRepository.DidNotReceive().UpsertAsync(
            Arg.Any<Guid>(),
            Arg.Any<float[]>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WhenVectorUpsertFails_ShouldStillSaveToFileSystem()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "vector_error_test",
            "描述",
            "内容"
        );

        var mockEmbedding = new float[] { 0.1f, 0.2f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        _mockVectorRepository.UpsertAsync(
            Arg.Any<Guid>(),
            Arg.Any<float[]>(),
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>())
            .Throws(new Exception("向量数据库连接失败"));

        // Act
        var result = await _repository.SaveAsync(memory);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(memory.Id);

        // 验证文件已保存
        var filePath = Path.Combine(_tempDirectory, "feedback", "vector_error_test.md");
        File.Exists(filePath).Should().BeTrue();

        // 验证 EmbeddingClient 被调用
        await _mockEmbeddingClient.Received(1).GenerateEmbeddingAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_WithEmptyTags_ShouldPassEmptyStringInMetadata()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Knowledge,
            "no_tags",
            "描述",
            "内容",
            new List<string>() // 空标签列表
        );

        var mockEmbedding = new float[] { 0.1f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        // Act
        await _repository.SaveAsync(memory);

        // Assert
        await _mockVectorRepository.Received(1).UpsertAsync(
            Arg.Any<Guid>(),
            Arg.Any<float[]>(),
            Arg.Is<Dictionary<string, object>>(m => m["tags"].ToString() == ""),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ShouldUseSpaceSeparator_NotNewline()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Reference,
            "space_test",
            "desc",
            "content"
        );

        var mockEmbedding = new float[] { 0.1f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        string? capturedText = null;
        await _mockEmbeddingClient.GenerateEmbeddingAsync(
            Arg.Do<string>(text => capturedText = text),
            Arg.Any<CancellationToken>());

        // Act
        await _repository.SaveAsync(memory);

        // Assert
        capturedText.Should().NotBeNull();
        capturedText.Should().Be("space_test desc content");
        capturedText.Should().NotContain("\n");
    }

    #endregion

    #region DeleteAsync 双写测试

    [Fact]
    public async Task DeleteAsync_WhenExists_ShouldCallVectorRepository()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "to_delete",
            "描述",
            "内容"
        );

        var mockEmbedding = new float[] { 0.1f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        await _repository.SaveAsync(memory);

        // Act
        var result = await _repository.DeleteAsync(memory.Id);

        // Assert
        result.Should().BeTrue();

        await _mockVectorRepository.Received(1).DeleteAsync(
            Arg.Is<Guid>(id => id == memory.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenVectorDeleteFails_ShouldStillDeleteFile()
    {
        // Arrange
        var memory = Core.Models.Memory.Create(
            MemoryType.Project,
            "delete_error_test",
            "描述",
            "内容"
        );

        var mockEmbedding = new float[] { 0.1f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        await _repository.SaveAsync(memory);

        _mockVectorRepository.DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>())
            .Throws(new Exception("向量删除失败"));

        // Act
        var result = await _repository.DeleteAsync(memory.Id);

        // Assert
        result.Should().BeTrue();

        // 验证文件已删除
        var filePath = Path.Combine(_tempDirectory, "project", "delete_error_test.md");
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenNotExists_ShouldNotCallVectorRepository()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.DeleteAsync(nonExistentId);

        // Assert
        result.Should().BeFalse();

        await _mockVectorRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateAsync 双写测试

    [Fact]
    public async Task UpdateAsync_ShouldUpdateVectorWithNewEmbedding()
    {
        // Arrange
        var original = Core.Models.Memory.Create(
            MemoryType.User,
            "update_test",
            "原始描述",
            "原始内容"
        );

        var originalEmbedding = new float[] { 0.1f, 0.2f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(originalEmbedding);

        await _repository.SaveAsync(original);

        // 重置 mock 调用计数
        _mockEmbeddingClient.ClearReceivedCalls();
        _mockVectorRepository.ClearReceivedCalls();

        var updated = original with
        {
            Description = "更新后的描述",
            Content = "更新后的内容"
        };

        var newEmbedding = new float[] { 0.3f, 0.4f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(newEmbedding);

        // Act
        await _repository.UpdateAsync(updated);

        // Assert
        await _mockEmbeddingClient.Received(1).GenerateEmbeddingAsync(
            Arg.Is<string>(text => text.Contains("更新后的描述") && text.Contains("更新后的内容")),
            Arg.Any<CancellationToken>());

        await _mockVectorRepository.Received(1).UpsertAsync(
            Arg.Is<Guid>(id => id == original.Id),
            Arg.Is<float[]>(e => e == newEmbedding),
            Arg.Is<Dictionary<string, object>>(m => m["description"].ToString() == "更新后的描述"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region 可选依赖测试

    [Fact]
    public async Task SaveAsync_WithNullVectorRepository_ShouldOnlySaveToFileSystem()
    {
        // Arrange - 创建没有向量依赖的仓储
        var options = Options.Create(new MemoryOptions
        {
            RootDirectory = _tempDirectory
        });

        var repositoryWithoutVector = new MemoryRepository(
            options,
            NullLogger<MemoryRepository>.Instance,
            vectorRepository: null,
            embeddingClient: null);

        var memory = Core.Models.Memory.Create(
            MemoryType.User,
            "no_vector",
            "描述",
            "内容"
        );

        // Act
        var result = await repositoryWithoutVector.SaveAsync(memory);

        // Assert
        result.Should().NotBeNull();

        // 验证文件已保存
        var filePath = Path.Combine(_tempDirectory, "user", "no_vector.md");
        File.Exists(filePath).Should().BeTrue();

        // 验证 mock 未被调用（因为它们是 null）
        await _mockEmbeddingClient.DidNotReceive().GenerateEmbeddingAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithNullVectorRepository_ShouldOnlyDeleteFile()
    {
        // Arrange - 先用有向量的仓储保存
        var memory = Core.Models.Memory.Create(
            MemoryType.Feedback,
            "delete_no_vector",
            "描述",
            "内容"
        );

        var mockEmbedding = new float[] { 0.1f };
        _mockEmbeddingClient.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(mockEmbedding);

        await _repository.SaveAsync(memory);

        // 创建没有向量依赖的仓储
        var options = Options.Create(new MemoryOptions
        {
            RootDirectory = _tempDirectory
        });

        var repositoryWithoutVector = new MemoryRepository(
            options,
            NullLogger<MemoryRepository>.Instance,
            vectorRepository: null,
            embeddingClient: null);

        // Act
        var result = await repositoryWithoutVector.DeleteAsync(memory.Id);

        // Assert
        result.Should().BeTrue();

        // 验证文件已删除
        var filePath = Path.Combine(_tempDirectory, "feedback", "delete_no_vector.md");
        File.Exists(filePath).Should().BeFalse();
    }

    #endregion
}
