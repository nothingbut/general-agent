using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Infrastructure.VectorDB;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace GeneralAgent.Integration.Tests.VectorDB;

/// <summary>
/// Qdrant VectorDB 集成测试
/// 测试与真实 Qdrant 服务的交互
/// </summary>
[Collection("Qdrant")]
[Trait("Category", "Integration")]
public sealed class QdrantVectorRepositoryIntegrationTests : IAsyncLifetime
{
    private const string QdrantUrl = "http://localhost:6333";
    private const string TestCollectionName = "integration_test";
    private const int QdrantGrpcPort = 6334; // gRPC 端口

    private readonly IVectorRepository _repository;
    private readonly IQdrantClient _client;
    private readonly List<Guid> _testMemoryIds = new();
    private string? _skipReason;

    public QdrantVectorRepositoryIntegrationTests()
    {
        // 配置真实服务
        var options = Options.Create(new VectorDBOptions
        {
            Url = QdrantUrl,
            CollectionName = TestCollectionName,
            HealthCheckCacheSeconds = 5
        });

        // QdrantClient 使用 gRPC，需要主机和端口参数（gRPC 默认端口是 6334）
        var qdrantClient = new QdrantClient("localhost", QdrantGrpcPort);
        _client = new QdrantClientWrapper(qdrantClient);
        var logger = NullLogger<QdrantVectorRepository>.Instance;

        _repository = new QdrantVectorRepository(_client, options, logger);
    }

    public async Task InitializeAsync()
    {
        // 检查 Qdrant 是否可用
        var isQdrantAvailable = await IsQdrantAvailableAsync();

        if (!isQdrantAvailable)
        {
            _skipReason = "Qdrant 服务不可用（http://localhost:6333）。请运行: docker run -d -p 6333:6333 qdrant/qdrant";
            return;
        }

        try
        {
            // 确保测试集合存在
            await EnsureCollectionExistsAsync();
        }
        catch (Exception ex)
        {
            _skipReason = $"初始化集合失败: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        // 清理测试数据
        foreach (var id in _testMemoryIds)
        {
            try
            {
                await _repository.DeleteAsync(id);
            }
            catch
            {
                // 忽略删除错误
            }
        }

        // 等待一下确保删除完成
        await Task.Delay(100);
    }

    /// <summary>
    /// 检查 Qdrant 服务是否可用
    /// </summary>
    private static async Task<bool> IsQdrantAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{QdrantUrl}/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 确保测试集合存在
    /// </summary>
    private async Task EnsureCollectionExistsAsync()
    {
        try
        {
            // 尝试获取集合信息
            await _client.GetCollectionInfoAsync(TestCollectionName);
        }
        catch
        {
            // 集合不存在，创建它
            var qdrantClient = new QdrantClient("localhost", QdrantGrpcPort);
            await qdrantClient.CreateCollectionAsync(
                collectionName: TestCollectionName,
                vectorsConfig: new Qdrant.Client.Grpc.VectorParams
                {
                    Size = 768,
                    Distance = Qdrant.Client.Grpc.Distance.Cosine
                });
        }
    }

    /// <summary>
    /// 跳过测试如果 Qdrant 不可用
    /// </summary>
    private void SkipIfQdrantUnavailable()
    {
        if (_skipReason != null)
        {
            throw new SkipTestException(_skipReason);
        }
    }

    /// <summary>
    /// 生成测试用的向量（768维）
    /// </summary>
    private static float[] GenerateTestVector(int seed = 0)
    {
        var vector = new float[768];
        var random = new Random(seed);

        for (int i = 0; i < 768; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1); // -1 到 1 之间
        }

        // 归一化向量
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        for (int i = 0; i < 768; i++)
        {
            vector[i] /= (float)magnitude;
        }

        return vector;
    }

    /// <summary>
    /// 测试：存储并搜索向量（端到端）
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task UpsertAndSearch_WorksEndToEnd()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        var memoryId = Guid.NewGuid();
        _testMemoryIds.Add(memoryId);

        var embedding = GenerateTestVector(seed: 42);
        var metadata = new Dictionary<string, object>
        {
            ["type"] = "test",
            ["name"] = "integration_test",
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        };

        // Act - Upsert
        await _repository.UpsertAsync(memoryId, embedding, metadata);

        // 等待索引完成
        await Task.Delay(500);

        // Act - Search
        var results = await _repository.SearchAsync(embedding, topK: 5);

        // Assert
        results.Should().NotBeEmpty();
        results[0].MemoryId.Should().Be(memoryId);
        results[0].Score.Should().BeGreaterThan(0.99); // 相同向量应该得分接近 1
        results[0].Metadata["type"].Should().Be("test");
        results[0].Metadata["name"].Should().Be("integration_test");
    }

    /// <summary>
    /// 测试：使用过滤条件搜索
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task SearchWithFilter_ReturnsFilteredResults()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        // 插入多个向量，使用不同的元数据
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        _testMemoryIds.AddRange(new[] { id1, id2, id3 });

        var vector1 = GenerateTestVector(seed: 100);
        var vector2 = GenerateTestVector(seed: 200);
        var vector3 = GenerateTestVector(seed: 300);

        await _repository.UpsertAsync(id1, vector1, new Dictionary<string, object>
        {
            ["category"] = "A",
            ["priority"] = 1
        });

        await _repository.UpsertAsync(id2, vector2, new Dictionary<string, object>
        {
            ["category"] = "B",
            ["priority"] = 2
        });

        await _repository.UpsertAsync(id3, vector3, new Dictionary<string, object>
        {
            ["category"] = "A",
            ["priority"] = 3
        });

        // 等待索引完成
        await Task.Delay(500);

        // Act - 搜索 category=A 的向量
        var filters = new Dictionary<string, object>
        {
            ["category"] = "A"
        };

        var results = await _repository.SearchAsync(vector1, topK: 10, filters: filters);

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountLessThanOrEqualTo(2); // 只有 id1 和 id3
        results.Should().OnlyContain(r => r.Metadata["category"].ToString() == "A");

        var resultIds = results.Select(r => r.MemoryId).ToList();
        resultIds.Should().Contain(id1);
        resultIds.Should().NotContain(id2); // id2 是 category B，应被过滤
    }

    /// <summary>
    /// 测试：删除向量并验证
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task DeleteAndVerify_RemovesVector()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        var memoryId = Guid.NewGuid();
        _testMemoryIds.Add(memoryId);

        var embedding = GenerateTestVector(seed: 500);
        var metadata = new Dictionary<string, object>
        {
            ["type"] = "deletable"
        };

        // 插入向量
        await _repository.UpsertAsync(memoryId, embedding, metadata);
        await Task.Delay(500);

        // 验证向量存在
        var resultsBeforeDelete = await _repository.SearchAsync(embedding, topK: 5);
        resultsBeforeDelete.Should().NotBeEmpty();
        resultsBeforeDelete[0].MemoryId.Should().Be(memoryId);

        // Act - 删除向量
        await _repository.DeleteAsync(memoryId);
        await Task.Delay(500); // 等待删除完成

        // Assert - 验证向量已删除
        var resultsAfterDelete = await _repository.SearchAsync(embedding, topK: 5);

        // 搜索结果中不应包含已删除的向量
        var deletedVector = resultsAfterDelete.FirstOrDefault(r => r.MemoryId == memoryId);
        deletedVector.Should().BeNull();
    }

    /// <summary>
    /// 测试：健康检查
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task IsHealthy_ReturnsTrue()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        // Act
        var isHealthy = await _repository.IsHealthyAsync();

        // Assert
        isHealthy.Should().BeTrue();
    }

    /// <summary>
    /// 测试：健康检查缓存
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task IsHealthy_UsesCaching()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        // Act - 第一次调用
        var startTime = DateTime.UtcNow;
        var isHealthy1 = await _repository.IsHealthyAsync();
        var firstCallDuration = DateTime.UtcNow - startTime;

        // 立即第二次调用（应该使用缓存）
        startTime = DateTime.UtcNow;
        var isHealthy2 = await _repository.IsHealthyAsync();
        var secondCallDuration = DateTime.UtcNow - startTime;

        // Assert
        isHealthy1.Should().BeTrue();
        isHealthy2.Should().BeTrue();

        // 第二次调用应该明显更快（使用缓存）
        secondCallDuration.Should().BeLessThan(firstCallDuration);
        secondCallDuration.Should().BeLessThan(TimeSpan.FromMilliseconds(10)); // 缓存调用应该很快
    }

    /// <summary>
    /// 测试：获取集合统计信息
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task GetStats_ReturnsValidStats()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        // 插入一些测试向量
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _testMemoryIds.AddRange(new[] { id1, id2 });

        await _repository.UpsertAsync(id1, GenerateTestVector(1), new Dictionary<string, object> { ["test"] = "1" });
        await _repository.UpsertAsync(id2, GenerateTestVector(2), new Dictionary<string, object> { ["test"] = "2" });
        await Task.Delay(500);

        // Act
        var stats = await _repository.GetStatsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.VectorCount.Should().BeGreaterThanOrEqualTo(2);
        stats.Dimensions.Should().Be(768);
        stats.IndexType.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// 测试：更新已存在的向量
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task Upsert_UpdatesExistingVector()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        var memoryId = Guid.NewGuid();
        _testMemoryIds.Add(memoryId);

        var initialVector = GenerateTestVector(seed: 1000);
        var initialMetadata = new Dictionary<string, object>
        {
            ["version"] = 1,
            ["status"] = "initial"
        };

        // 插入初始向量
        await _repository.UpsertAsync(memoryId, initialVector, initialMetadata);
        await Task.Delay(500);

        // Act - 更新相同 ID 的向量
        var updatedVector = GenerateTestVector(seed: 2000);
        var updatedMetadata = new Dictionary<string, object>
        {
            ["version"] = 2,
            ["status"] = "updated"
        };

        await _repository.UpsertAsync(memoryId, updatedVector, updatedMetadata);
        await Task.Delay(500);

        // Assert - 搜索应该返回更新后的向量
        var results = await _repository.SearchAsync(updatedVector, topK: 5);

        results.Should().NotBeEmpty();
        results[0].MemoryId.Should().Be(memoryId);
        results[0].Metadata["version"].Should().Be(2L); // Qdrant 返回 long
        results[0].Metadata["status"].Should().Be("updated");
    }

    /// <summary>
    /// 测试：批量插入多个向量
    /// </summary>
    [Fact(Skip = "需要运行 Qdrant 服务: docker run -d -p 6333:6333 -p 6334:6334 qdrant/qdrant")]
    public async Task UpsertMultipleVectors_AllSucceed()
    {
        // Arrange
        SkipIfQdrantUnavailable();

        var vectors = new List<(Guid id, float[] vector, Dictionary<string, object> metadata)>();

        for (int i = 0; i < 10; i++)
        {
            var id = Guid.NewGuid();
            _testMemoryIds.Add(id);

            vectors.Add((
                id,
                GenerateTestVector(seed: 3000 + i),
                new Dictionary<string, object> { ["index"] = i }
            ));
        }

        // Act - 批量插入
        foreach (var (id, vector, metadata) in vectors)
        {
            await _repository.UpsertAsync(id, vector, metadata);
        }

        await Task.Delay(1000); // 等待所有向量索引完成

        // Assert - 搜索每个向量，验证都能找到
        foreach (var (id, vector, _) in vectors)
        {
            var results = await _repository.SearchAsync(vector, topK: 1);
            results.Should().NotBeEmpty();
            results[0].MemoryId.Should().Be(id);
        }
    }
}

/// <summary>
/// 用于跳过测试的异常类
/// </summary>
internal class SkipTestException : Exception
{
    public SkipTestException(string message) : base(message) { }
}
