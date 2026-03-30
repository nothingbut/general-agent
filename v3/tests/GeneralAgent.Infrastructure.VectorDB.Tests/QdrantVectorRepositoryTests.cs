using FluentAssertions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Qdrant.Client.Grpc;

namespace GeneralAgent.Infrastructure.VectorDB.Tests;

/// <summary>
/// QdrantVectorRepository 单元测试
/// </summary>
public sealed class QdrantVectorRepositoryTests
{
    private readonly IQdrantClient _mockClient;
    private readonly IOptions<VectorDBOptions> _options;
    private readonly ILogger<QdrantVectorRepository> _logger;
    private readonly QdrantVectorRepository _repository;

    public QdrantVectorRepositoryTests()
    {
        _mockClient = Substitute.For<IQdrantClient>();
        _options = Options.Create(new VectorDBOptions
        {
            Provider = "Qdrant",
            Url = "http://localhost:6333",
            CollectionName = "test_collection",
            EnableFallback = true,
            HealthCheckCacheSeconds = 60
        });
        _logger = Substitute.For<ILogger<QdrantVectorRepository>>();
        _repository = new QdrantVectorRepository(_mockClient, _options, _logger);
    }

    #region Constructor Tests

    /// <summary>
    /// 测试：Null IQdrantClient 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new QdrantVectorRepository(null!, _options, _logger));
        exception.ParamName.Should().Be("client");
    }

    /// <summary>
    /// 测试：Null Options 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new QdrantVectorRepository(_mockClient, null!, _logger));
        exception.ParamName.Should().Be("options");
    }

    /// <summary>
    /// 测试：Null Logger 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => new QdrantVectorRepository(_mockClient, _options, null!));
        exception.ParamName.Should().Be("logger");
    }

    #endregion

    #region UpsertAsync Tests

    /// <summary>
    /// 测试：成功插入向量
    /// </summary>
    [Fact]
    public async Task UpsertAsync_ValidInput_Succeeds()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var metadata = new Dictionary<string, object>
        {
            ["text"] = "Hello world",
            ["timestamp"] = 1234567890L
        };

        _mockClient
            .UpsertAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<PointStruct>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UpdateResult()));

        // Act
        await _repository.UpsertAsync(memoryId, embedding, metadata);

        // Assert
        await _mockClient.Received(1).UpsertAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<PointStruct>>(points => points.Count == 1),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 测试：空元数据插入成功
    /// </summary>
    [Fact]
    public async Task UpsertAsync_EmptyMetadata_Succeeds()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f };
        var metadata = new Dictionary<string, object>();

        _mockClient
            .UpsertAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<PointStruct>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UpdateResult()));

        // Act
        await _repository.UpsertAsync(memoryId, embedding, metadata);

        // Assert
        await _mockClient.Received(1).UpsertAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<PointStruct>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 测试：Qdrant 错误时抛出 VectorRepositoryException
    /// </summary>
    [Fact]
    public async Task UpsertAsync_QdrantError_ThrowsVectorRepositoryException()
    {
        // Arrange
        var memoryId = Guid.NewGuid();
        var embedding = new float[] { 0.1f, 0.2f };
        var metadata = new Dictionary<string, object>();

        _mockClient
            .UpsertAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<PointStruct>>(),
                Arg.Any<CancellationToken>())
            .Throws(new Exception("Qdrant connection failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<VectorRepositoryException>(
            async () => await _repository.UpsertAsync(memoryId, embedding, metadata));
        exception.Message.Should().Contain("存储向量失败");
        exception.InnerException.Should().NotBeNull();
        exception.InnerException!.Message.Should().Contain("Qdrant connection failed");
    }

    #endregion

    #region SearchAsync Tests

    /// <summary>
    /// 测试：成功搜索返回结果
    /// </summary>
    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsResults()
    {
        // Arrange
        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
        var memoryId1 = Guid.NewGuid();
        var memoryId2 = Guid.NewGuid();

        var scoredPoints = new[]
        {
            new ScoredPoint
            {
                Id = new PointId { Uuid = memoryId1.ToString() },
                Score = 0.95f,
                Payload =
                {
                    ["text"] = new Value { StringValue = "Result 1" }
                }
            },
            new ScoredPoint
            {
                Id = new PointId { Uuid = memoryId2.ToString() },
                Score = 0.85f,
                Payload =
                {
                    ["text"] = new Value { StringValue = "Result 2" }
                }
            }
        };

        _mockClient
            .SearchAsync(
                Arg.Any<string>(),
                Arg.Any<float[]>(),
                Arg.Any<Filter?>(),
                Arg.Any<ulong>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(scoredPoints));

        // Act
        var results = await _repository.SearchAsync(queryVector, topK: 2);

        // Assert
        results.Should().NotBeNull();
        results.Count.Should().Be(2);
        results[0].MemoryId.Should().Be(memoryId1);
        results[0].Score.Should().BeApproximately(0.95, 0.001);
        results[0].Metadata["text"].Should().Be("Result 1");
        results[1].MemoryId.Should().Be(memoryId2);
        results[1].Score.Should().BeApproximately(0.85, 0.001);
    }

    /// <summary>
    /// 测试：无结果返回空列表
    /// </summary>
    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyList()
    {
        // Arrange
        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };

        _mockClient
            .SearchAsync(
                Arg.Any<string>(),
                Arg.Any<float[]>(),
                Arg.Any<Filter?>(),
                Arg.Any<ulong>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Array.Empty<ScoredPoint>()));

        // Act
        var results = await _repository.SearchAsync(queryVector);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    /// <summary>
    /// 测试：带过滤条件搜索成功
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithFilters_Succeeds()
    {
        // Arrange
        var queryVector = new float[] { 0.1f, 0.2f, 0.3f };
        var filters = new Dictionary<string, object>
        {
            ["category"] = "test",
            ["active"] = true
        };

        var memoryId = Guid.NewGuid();
        var scoredPoints = new[]
        {
            new ScoredPoint
            {
                Id = new PointId { Uuid = memoryId.ToString() },
                Score = 0.9f,
                Payload =
                {
                    ["category"] = new Value { StringValue = "test" }
                }
            }
        };

        _mockClient
            .SearchAsync(
                Arg.Any<string>(),
                Arg.Any<float[]>(),
                Arg.Is<Filter?>(f => f != null),
                Arg.Any<ulong>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(scoredPoints));

        // Act
        var results = await _repository.SearchAsync(queryVector, topK: 5, filters: filters);

        // Assert
        results.Should().NotBeNull();
        results.Count.Should().Be(1);
        results[0].MemoryId.Should().Be(memoryId);
    }

    /// <summary>
    /// 测试：Qdrant 错误时抛出 VectorRepositoryException
    /// </summary>
    [Fact]
    public async Task SearchAsync_QdrantError_ThrowsVectorRepositoryException()
    {
        // Arrange
        var queryVector = new float[] { 0.1f, 0.2f };

        _mockClient
            .SearchAsync(
                Arg.Any<string>(),
                Arg.Any<float[]>(),
                Arg.Any<Filter?>(),
                Arg.Any<ulong>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Throws(new Exception("Search failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<VectorRepositoryException>(
            async () => await _repository.SearchAsync(queryVector));
        exception.Message.Should().Contain("向量搜索失败");
    }

    #endregion

    #region DeleteAsync Tests

    /// <summary>
    /// 测试：成功删除向量
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ValidId_Succeeds()
    {
        // Arrange
        var memoryId = Guid.NewGuid();

        _mockClient
            .DeleteAsync(
                Arg.Any<string>(),
                Arg.Any<Filter>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UpdateResult()));

        // Act
        await _repository.DeleteAsync(memoryId);

        // Assert
        await _mockClient.Received(1).DeleteAsync(
            Arg.Any<string>(),
            Arg.Any<Filter>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 测试：Qdrant 错误时抛出 VectorRepositoryException
    /// </summary>
    [Fact]
    public async Task DeleteAsync_QdrantError_ThrowsVectorRepositoryException()
    {
        // Arrange
        var memoryId = Guid.NewGuid();

        _mockClient
            .DeleteAsync(
                Arg.Any<string>(),
                Arg.Any<Filter>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Throws(new Exception("Delete failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<VectorRepositoryException>(
            async () => await _repository.DeleteAsync(memoryId));
        exception.Message.Should().Contain("删除向量失败");
        exception.InnerException.Should().NotBeNull();
    }

    #endregion

    #region IsHealthyAsync Tests

    /// <summary>
    /// 测试：健康检查成功
    /// </summary>
    [Fact]
    public async Task IsHealthyAsync_QdrantHealthy_ReturnsTrue()
    {
        // Arrange
        _mockClient
            .GetCollectionInfoAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CollectionInfo()));

        // Act
        var isHealthy = await _repository.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
        await _mockClient.Received(1).GetCollectionInfoAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 测试：健康检查失败
    /// </summary>
    [Fact]
    public async Task IsHealthyAsync_QdrantDown_ReturnsFalse()
    {
        // Arrange
        _mockClient
            .GetCollectionInfoAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Throws(new Exception("Connection failed"));

        // Act
        var isHealthy = await _repository.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeFalse();
    }

    /// <summary>
    /// 测试：健康检查使用缓存（避免频繁调用）
    /// </summary>
    [Fact]
    public async Task IsHealthyAsync_UsesCaching_AvoidsDuplicateCalls()
    {
        // Arrange
        _mockClient
            .GetCollectionInfoAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CollectionInfo()));

        // Act
        var isHealthy1 = await _repository.IsHealthyAsync();
        var isHealthy2 = await _repository.IsHealthyAsync();
        var isHealthy3 = await _repository.IsHealthyAsync();

        // Assert
        isHealthy1.Should().BeTrue();
        isHealthy2.Should().BeTrue();
        isHealthy3.Should().BeTrue();

        // 由于缓存，只应调用一次
        await _mockClient.Received(1).GetCollectionInfoAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// 测试：健康检查缓存过期后重新检查
    /// </summary>
    [Fact]
    public async Task IsHealthyAsync_CacheExpired_PerformsNewCheck()
    {
        // Arrange
        var shortCacheOptions = Options.Create(new VectorDBOptions
        {
            CollectionName = "test",
            HealthCheckCacheSeconds = 0 // 缓存立即过期
        });
        var repository = new QdrantVectorRepository(_mockClient, shortCacheOptions, _logger);

        _mockClient
            .GetCollectionInfoAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new CollectionInfo()));

        // Act
        var isHealthy1 = await repository.IsHealthyAsync();
        await Task.Delay(10); // 确保缓存过期
        var isHealthy2 = await repository.IsHealthyAsync();

        // Assert
        isHealthy1.Should().BeTrue();
        isHealthy2.Should().BeTrue();

        // 缓存过期，应调用两次
        await _mockClient.Received(2).GetCollectionInfoAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetStatsAsync Tests

    /// <summary>
    /// 测试：成功获取集合统计信息
    /// </summary>
    [Fact]
    public async Task GetStatsAsync_ValidCollection_ReturnsStats()
    {
        // Arrange
        var collectionInfo = new CollectionInfo
        {
            PointsCount = 1000,
            Config = new CollectionConfig
            {
                Params = new CollectionParams
                {
                    VectorsConfig = new VectorsConfig
                    {
                        Params = new VectorParams
                        {
                            Size = 768,
                            HnswConfig = new HnswConfigDiff()
                        }
                    }
                }
            }
        };

        _mockClient
            .GetCollectionInfoAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(collectionInfo));

        // Act
        var stats = await _repository.GetStatsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.VectorCount.Should().Be(1000);
        stats.Dimensions.Should().Be(768);
        stats.IndexType.Should().Be("HNSW");
    }

    /// <summary>
    /// 测试：Qdrant 错误时抛出 VectorRepositoryException
    /// </summary>
    [Fact]
    public async Task GetStatsAsync_QdrantError_ThrowsVectorRepositoryException()
    {
        // Arrange
        _mockClient
            .GetCollectionInfoAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Throws(new Exception("Failed to get stats"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<VectorRepositoryException>(
            async () => await _repository.GetStatsAsync());
        exception.Message.Should().Contain("获取集合统计信息失败");
    }

    #endregion
}
