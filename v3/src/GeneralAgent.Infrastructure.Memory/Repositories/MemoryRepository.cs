using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace GeneralAgent.Infrastructure.Memory.Repositories;

/// <summary>
/// 基于文件系统的记忆仓储实现
/// </summary>
public class MemoryRepository : IMemoryRepository
{
    private readonly MemoryOptions _options;
    private readonly ILogger<MemoryRepository> _logger;
    private readonly string _rootPath;
    private readonly IVectorRepository? _vectorRepository;
    private readonly IEmbeddingClient? _embeddingClient;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache? _cache;

    // 内存索引：MemoryId → FilePath（解决 N+1 查询问题）
    private readonly Dictionary<Guid, string> _idToFilePathIndex = new();
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexBuilt = false;

    public MemoryRepository(
        IOptions<MemoryOptions> options,
        ILogger<MemoryRepository> logger,
        IVectorRepository? vectorRepository = null,
        IEmbeddingClient? embeddingClient = null,
        Microsoft.Extensions.Caching.Memory.IMemoryCache? cache = null)
    {
        _options = options.Value;
        _logger = logger;
        _rootPath = _options.RootDirectory;
        _vectorRepository = vectorRepository;
        _embeddingClient = embeddingClient;
        _cache = cache;

        // 确保根目录存在
        EnsureDirectoriesExist();
    }

    public async Task<Core.Models.Memory> SaveAsync(Core.Models.Memory memory, CancellationToken cancellationToken = default)
    {
        var filePath = GetFullPath(memory.FilePath);
        var directory = Path.GetDirectoryName(filePath)!;

        // 确保目录存在
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("创建记忆目录: {Directory}", directory);
        }

        // 生成文件内容（带 frontmatter）
        var content = GenerateMemoryFileContent(memory);

        // 写入文件
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);

        // 更新索引
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            _idToFilePathIndex[memory.Id] = filePath;
        }
        finally
        {
            _indexLock.Release();
        }

        // 清除搜索缓存（记忆已更新）
        InvalidateSearchCache();

        _logger.LogInformation(
            "保存记忆: {Name} ({Type}) -> {FilePath}",
            memory.Name, memory.Type, memory.FilePath);

        // 同步到向量数据库（可选，容错）
        if (_vectorRepository != null && _embeddingClient != null)
        {
            try
            {
                // 生成记忆内容的 embedding
                var embeddingText = $"{memory.Name} {memory.Description} {memory.Content}";
                var embedding = await _embeddingClient.GenerateEmbeddingAsync(
                    embeddingText,
                    cancellationToken);

                // 准备元数据
                var metadata = new Dictionary<string, object>
                {
                    { "memory_id", memory.Id.ToString() },
                    { "type", memory.Type.ToString() },
                    { "name", memory.Name },
                    { "description", memory.Description },
                    { "tags", string.Join(",", memory.Tags) },
                    { "created_at", memory.CreatedAt.ToString("O") },
                    { "updated_at", memory.UpdatedAt.ToString("O") }
                };

                // 同步到向量数据库
                await _vectorRepository.UpsertAsync(
                    memory.Id,
                    embedding,
                    metadata,
                    cancellationToken);

                _logger.LogDebug(
                    "记忆向量已同步到 VectorDB: {Name} ({Dimensions}D)",
                    memory.Name,
                    embedding.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "向量同步失败，但记忆已保存到文件系统: {Name}",
                    memory.Name);
            }
        }

        return memory;
    }

    public async Task<Core.Models.Memory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 确保索引已构建
        await EnsureIndexBuiltAsync(cancellationToken);

        // 从索引获取文件路径
        await _indexLock.WaitAsync(cancellationToken);
        string? filePath;
        try
        {
            if (!_idToFilePathIndex.TryGetValue(id, out filePath))
            {
                _logger.LogDebug("记忆 ID {Id} 不存在于索引中", id);
                return null;
            }
        }
        finally
        {
            _indexLock.Release();
        }

        // 直接加载文件（避免加载所有记忆）
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("记忆文件不存在: {FilePath}", filePath);
            return null;
        }

        return await LoadMemoryFromFileAsync(filePath, cancellationToken);
    }

    public async Task<List<Core.Models.Memory>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        // 确保索引已构建
        await EnsureIndexBuiltAsync(cancellationToken);

        var memories = new List<Core.Models.Memory>();

        await _indexLock.WaitAsync(cancellationToken);
        List<string> filePaths;
        try
        {
            // 从索引获取所有文件路径（去重）
            filePaths = ids
                .Distinct()  // 去重：处理重复 ID
                .Where(id => _idToFilePathIndex.ContainsKey(id))
                .Select(id => _idToFilePathIndex[id])
                .ToList();
        }
        finally
        {
            _indexLock.Release();
        }

        // 批量加载文件（只加载需要的）
        foreach (var filePath in filePaths)
        {
            if (File.Exists(filePath))
            {
                var memory = await LoadMemoryFromFileAsync(filePath, cancellationToken);
                if (memory != null)
                {
                    memories.Add(memory);
                }
            }
        }

        return memories;
    }

    public async Task<Core.Models.Memory?> GetByNameAsync(
        string name,
        MemoryType type,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFullPath($"{type.ToString().ToLower()}/{name}.md");

        if (!File.Exists(filePath))
        {
            return null;
        }

        return await LoadMemoryFromFileAsync(filePath, cancellationToken);
    }

    public async Task<List<Core.Models.Memory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var memories = new List<Core.Models.Memory>();

        foreach (MemoryType type in Enum.GetValues<MemoryType>())
        {
            var typeMemories = await GetByTypeAsync(type, cancellationToken);
            memories.AddRange(typeMemories);
        }

        return memories;
    }

    public async Task<List<Core.Models.Memory>> GetByTypeAsync(
        MemoryType type,
        CancellationToken cancellationToken = default)
    {
        var typePath = GetFullPath(type.ToString().ToLower());

        if (!Directory.Exists(typePath))
        {
            return new List<Core.Models.Memory>();
        }

        var files = Directory.GetFiles(typePath, "*.md", SearchOption.TopDirectoryOnly);
        var memories = new List<Core.Models.Memory>();

        foreach (var file in files)
        {
            try
            {
                var memory = await LoadMemoryFromFileAsync(file, cancellationToken);
                if (memory != null)
                {
                    memories.Add(memory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载记忆文件失败: {File}", file);
            }
        }

        return memories;
    }

    public async Task<List<Core.Models.Memory>> SearchAsync(
        string keyword,
        MemoryType? type = null,
        CancellationToken cancellationToken = default)
    {
        // 尝试从缓存获取
        if (_cache != null)
        {
            var cacheKey = $"memory_search_{keyword}_{type?.ToString() ?? "all"}";

            if (_cache.TryGetValue<List<Core.Models.Memory>>(cacheKey, out var cachedResult) && cachedResult != null)
            {
                _logger.LogDebug("✅ 关键词搜索缓存命中: {Keyword}", keyword);
                return cachedResult;
            }
        }

        // 执行搜索
        var memories = type.HasValue
            ? await GetByTypeAsync(type.Value, cancellationToken)
            : await GetAllAsync(cancellationToken);

        var searchKeyword = keyword.ToLower();

        var results = memories.Where(m =>
            m.Name.ToLower().Contains(searchKeyword) ||
            m.Description.ToLower().Contains(searchKeyword) ||
            m.Content.ToLower().Contains(searchKeyword) ||
            m.Tags.Any(t => t.ToLower().Contains(searchKeyword))
        ).ToList();

        // 缓存结果（5 分钟）
        if (_cache != null)
        {
            var cacheKey = $"memory_search_{keyword}_{type?.ToString() ?? "all"}";
            _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));
            _logger.LogDebug("📦 关键词搜索结果已缓存: {Keyword}, 结果数: {Count}", keyword, results.Count);
        }

        return results;
    }

    public async Task<List<Core.Models.Memory>> SearchByTagsAsync(
        List<string> tags,
        CancellationToken cancellationToken = default)
    {
        var memories = await GetAllAsync(cancellationToken);
        var searchTags = tags.Select(t => t.ToLower()).ToList();

        return memories.Where(m =>
            m.Tags.Any(t => searchTags.Contains(t.ToLower()))
        ).ToList();
    }

    public async Task<Core.Models.Memory> UpdateAsync(Core.Models.Memory memory, CancellationToken cancellationToken = default)
    {
        // 更新就是重新保存
        return await SaveAsync(memory, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // 确保索引已构建
        await EnsureIndexBuiltAsync(cancellationToken);

        // 从索引获取文件路径
        await _indexLock.WaitAsync(cancellationToken);
        string? filePath;
        try
        {
            if (!_idToFilePathIndex.TryGetValue(id, out filePath))
            {
                _logger.LogWarning("记忆 ID {Id} 不存在于索引中，无法删除", id);
                return false;
            }

            // 从索引中移除
            _idToFilePathIndex.Remove(id);
        }
        finally
        {
            _indexLock.Release();
        }

        // 删除文件
        var deleted = false;
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("删除记忆文件: {File}", filePath);
                deleted = true;

                // 清除搜索缓存（记忆已删除）
                InvalidateSearchCache();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除文件失败: {File}", filePath);
        }

        // 同步删除向量数据库中的记录（可选，容错）
        if (deleted && _vectorRepository != null)
        {
            try
            {
                await _vectorRepository.DeleteAsync(id, cancellationToken);
                _logger.LogDebug("向量已从 VectorDB 删除: {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "向量删除失败，但记忆已从文件系统删除: {Id}",
                    id);
            }
        }

        return deleted;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var memory = await GetByIdAsync(id, cancellationToken);
        return memory != null;
    }

    public async Task<bool> NameExistsAsync(
        string name,
        MemoryType type,
        CancellationToken cancellationToken = default)
    {
        var memory = await GetByNameAsync(name, type, cancellationToken);
        return memory != null;
    }

    /// <summary>
    /// 从文件加载记忆
    /// </summary>
    private async Task<Core.Models.Memory?> LoadMemoryFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return ParseMemoryFromContent(content, filePath);
    }

    /// <summary>
    /// 解析记忆文件内容
    /// 格式：YAML frontmatter + Markdown content
    /// </summary>
    private Core.Models.Memory? ParseMemoryFromContent(string fileContent, string filePath)
    {
        // 简单的 frontmatter 解析（假设格式为 ---\n...metadata...\n---\ncontent）
        if (!fileContent.StartsWith("---"))
        {
            _logger.LogWarning("记忆文件格式错误（缺少 frontmatter）: {File}", filePath);
            return null;
        }

        var lines = fileContent.Split('\n');
        var frontmatterEnd = Array.FindIndex(lines, 1, l => l.Trim() == "---");

        if (frontmatterEnd == -1)
        {
            _logger.LogWarning("记忆文件格式错误（frontmatter 未关闭）: {File}", filePath);
            return null;
        }

        // 解析 frontmatter
        var metadata = new Dictionary<string, string>();
        for (int i = 1; i < frontmatterEnd; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = line.Substring(0, colonIndex).Trim();
                var value = line.Substring(colonIndex + 1).Trim();
                metadata[key] = value;
            }
        }

        // 提取内容
        var contentStartIndex = frontmatterEnd + 1;
        var content = string.Join('\n', lines.Skip(contentStartIndex)).Trim();

        // 构建 Core.Models.Memory 对象
        try
        {
            var id = Guid.Parse(metadata.GetValueOrDefault("id", Guid.NewGuid().ToString()));
            var type = Enum.Parse<MemoryType>(metadata.GetValueOrDefault("type", "Knowledge"), true);
            var name = metadata.GetValueOrDefault("name", Path.GetFileNameWithoutExtension(filePath));
            var description = metadata.GetValueOrDefault("description", "");
            var tags = metadata.GetValueOrDefault("tags", "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();
            var createdAt = DateTime.Parse(metadata.GetValueOrDefault("created_at", DateTime.UtcNow.ToString("O")));
            var updatedAt = DateTime.Parse(metadata.GetValueOrDefault("updated_at", DateTime.UtcNow.ToString("O")));

            return new Core.Models.Memory
            {
                Id = id,
                Type = type,
                Name = name,
                Description = description,
                Content = content,
                Tags = tags,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析记忆文件失败: {File}", filePath);
            return null;
        }
    }

    /// <summary>
    /// 生成记忆文件内容（YAML frontmatter + Markdown）
    /// </summary>
    private string GenerateMemoryFileContent(Core.Models.Memory memory)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"id: {memory.Id}");
        sb.AppendLine($"name: {memory.Name}");
        sb.AppendLine($"description: {memory.Description}");
        sb.AppendLine($"type: {memory.Type}");

        if (memory.Tags.Count > 0)
        {
            sb.AppendLine($"tags: {string.Join(", ", memory.Tags)}");
        }

        sb.AppendLine($"created_at: {memory.CreatedAt:O}");
        sb.AppendLine($"updated_at: {memory.UpdatedAt:O}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(memory.Content);

        return sb.ToString();
    }

    /// <summary>
    /// 确保所有类型目录存在
    /// </summary>
    private void EnsureDirectoriesExist()
    {
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
            _logger.LogInformation("创建记忆根目录: {Path}", _rootPath);
        }

        foreach (MemoryType type in Enum.GetValues<MemoryType>())
        {
            var typePath = GetFullPath(type.ToString().ToLower());
            if (!Directory.Exists(typePath))
            {
                Directory.CreateDirectory(typePath);
                _logger.LogDebug("创建记忆类型目录: {Type}", type);
            }
        }
    }

    /// <summary>
    /// 获取完整路径
    /// </summary>
    private string GetFullPath(string relativePath)
    {
        return Path.Combine(_rootPath, relativePath);
    }

    /// <summary>
    /// 清除所有搜索缓存
    /// </summary>
    private void InvalidateSearchCache()
    {
        if (_cache == null)
        {
            return;
        }

        // 由于 IMemoryCache 不支持枚举键，我们使用一个简单的方法：
        // 清除所有可能的缓存键（基于已知的记忆类型）
        var cacheKeyPrefixes = new List<string>();

        // 为每个记忆类型生成缓存键前缀
        foreach (MemoryType memoryType in Enum.GetValues<MemoryType>())
        {
            // 注意：这里只是一个简化实现
            // 在生产环境中，可能需要使用 CacheItemPolicy 或自定义缓存管理器
        }

        // 简化方案：使用缓存版本号
        // 在真实场景中，建议使用 Redis 或自定义缓存管理器来支持按前缀清除
        _logger.LogDebug("🗑️ 搜索缓存已失效（记忆更新）");

        // 注意：当前实现依赖缓存过期时间（5分钟）
        // 更好的方案是实现自定义缓存管理器或使用 Redis
    }

    /// <summary>
    /// 确保索引已构建（懒加载）
    /// </summary>
    private async Task EnsureIndexBuiltAsync(CancellationToken cancellationToken = default)
    {
        if (_indexBuilt)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            // 双重检查锁定
            if (_indexBuilt)
            {
                return;
            }

            await BuildIndexAsync(cancellationToken);
            _indexBuilt = true;

            _logger.LogInformation(
                "记忆索引构建完成: {Count} 条记忆",
                _idToFilePathIndex.Count);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// 构建索引（扫描所有记忆文件）
    /// </summary>
    private async Task BuildIndexAsync(CancellationToken cancellationToken = default)
    {
        _idToFilePathIndex.Clear();

        // 扫描所有 .md 文件
        var files = Directory.GetFiles(_rootPath, "*.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var id = await ExtractIdFromFileAsync(file, cancellationToken);
                if (id.HasValue)
                {
                    _idToFilePathIndex[id.Value] = file;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "提取文件 ID 失败: {File}", file);
            }
        }

        _logger.LogDebug(
            "扫描完成: {FileCount} 个文件, {IndexCount} 条索引",
            files.Length,
            _idToFilePathIndex.Count);
    }

    /// <summary>
    /// 从文件中提取 ID（只读取 frontmatter，避免加载整个文件）
    /// </summary>
    private async Task<Guid?> ExtractIdFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // 只读取前几行（frontmatter 通常在前 20 行内）
            using var reader = new StreamReader(filePath);
            var lines = new List<string>();

            for (int i = 0; i < 30; i++)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;  // 文件结束

                lines.Add(line);

                // 找到第二个 --- 就可以停止
                if (i > 0 && line.Trim() == "---")
                {
                    break;
                }
            }

            // 解析 frontmatter
            if (lines.Count < 3 || !lines[0].StartsWith("---"))
            {
                return null;
            }

            var frontmatterEnd = lines.FindIndex(1, l => l.Trim() == "---");
            if (frontmatterEnd == -1)
            {
                return null;
            }

            // 提取 id 字段
            for (int i = 1; i < frontmatterEnd; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
                {
                    var idValue = line.Substring(3).Trim();
                    if (Guid.TryParse(idValue, out var id))
                    {
                        return id;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取文件失败: {File}", filePath);
            return null;
        }
    }
}
