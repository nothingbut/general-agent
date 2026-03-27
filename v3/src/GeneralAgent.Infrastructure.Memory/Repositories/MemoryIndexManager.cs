using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace GeneralAgent.Infrastructure.Memory.Repositories;

/// <summary>
/// 记忆索引管理器（管理 MEMORY.md）
/// </summary>
public class MemoryIndexManager : IMemoryIndexManager
{
    private readonly MemoryOptions _options;
    private readonly IMemoryRepository _memoryRepository;
    private readonly ILogger<MemoryIndexManager> _logger;
    private readonly string _indexFilePath;

    public MemoryIndexManager(
        IOptions<MemoryOptions> options,
        IMemoryRepository memoryRepository,
        ILogger<MemoryIndexManager> logger)
    {
        _options = options.Value;
        _memoryRepository = memoryRepository;
        _logger = logger;
        _indexFilePath = Path.Combine(_options.RootDirectory, _options.IndexFileName);
    }

    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始重建记忆索引...");

        // 获取所有记忆
        var allMemories = await _memoryRepository.GetAllAsync(cancellationToken);

        // 按类型分组
        var memoriesByType = allMemories.GroupBy(m => m.Type)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 生成索引内容
        var content = GenerateIndexContent(memoriesByType);

        // 写入索引文件
        await File.WriteAllTextAsync(_indexFilePath, content, Encoding.UTF8, cancellationToken);

        _logger.LogInformation(
            "记忆索引重建完成: {Count} 条记忆",
            allMemories.Count);
    }

    public async Task AddToIndexAsync(Core.Models.Memory memory, CancellationToken cancellationToken = default)
    {
        // 简单实现：重建索引
        // 优化方案：可以直接在索引文件中插入新条目
        await RebuildIndexAsync(cancellationToken);

        _logger.LogDebug("添加记忆到索引: {Name}", memory.Name);
    }

    public async Task RemoveFromIndexAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        // 简单实现：重建索引
        await RebuildIndexAsync(cancellationToken);

        _logger.LogDebug("从索引中移除记忆: {Id}", memoryId);
    }

    public async Task UpdateInIndexAsync(Core.Models.Memory memory, CancellationToken cancellationToken = default)
    {
        // 简单实现：重建索引
        await RebuildIndexAsync(cancellationToken);

        _logger.LogDebug("更新索引中的记忆: {Name}", memory.Name);
    }

    public async Task<List<MemoryIndex>> GetAllIndexEntriesAsync(CancellationToken cancellationToken = default)
    {
        var allMemories = await _memoryRepository.GetAllAsync(cancellationToken);
        return allMemories.Select(MemoryIndex.FromMemory).ToList();
    }

    public async Task<List<MemoryIndex>> GetIndexEntriesByTypeAsync(
        MemoryType type,
        CancellationToken cancellationToken = default)
    {
        var memories = await _memoryRepository.GetByTypeAsync(type, cancellationToken);
        return memories.Select(MemoryIndex.FromMemory).ToList();
    }

    public async Task<bool> ValidateIndexAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_indexFilePath))
        {
            _logger.LogWarning("索引文件不存在: {Path}", _indexFilePath);
            return false;
        }

        // 获取所有实际记忆
        var allMemories = await _memoryRepository.GetAllAsync(cancellationToken);
        var actualCoreMemoryIds = allMemories.Select(m => m.Id).ToHashSet();

        // 解析索引文件
        var indexContent = await File.ReadAllTextAsync(_indexFilePath, cancellationToken);
        var indexedCoreMemoryIds = ExtractCoreMemoryIdsFromIndex(indexContent);

        // 检查是否一致
        var missingInIndex = actualCoreMemoryIds.Except(indexedCoreMemoryIds).ToList();
        var missingInFiles = indexedCoreMemoryIds.Except(actualCoreMemoryIds).ToList();

        if (missingInIndex.Count > 0)
        {
            _logger.LogWarning(
                "索引中缺少 {Count} 条记忆",
                missingInIndex.Count);
        }

        if (missingInFiles.Count > 0)
        {
            _logger.LogWarning(
                "索引中包含 {Count} 条不存在的记忆",
                missingInFiles.Count);
        }

        var isValid = missingInIndex.Count == 0 && missingInFiles.Count == 0;

        if (!isValid && _options.AutoRebuildCorruptedIndex)
        {
            _logger.LogInformation("索引已损坏，自动重建...");
            await RebuildIndexAsync(cancellationToken);
            return true;
        }

        return isValid;
    }

    /// <summary>
    /// 生成索引文件内容
    /// </summary>
    private string GenerateIndexContent(Dictionary<MemoryType, List<Core.Models.Memory>> memoriesByType)
    {
        var sb = new StringBuilder();

        // 标题
        sb.AppendLine("# CoreMemory Index");
        sb.AppendLine();
        sb.AppendLine("这是长期记忆系统的索引文件，记录了所有已保存的记忆。");
        sb.AppendLine();

        // 统计信息
        var totalCount = memoriesByType.Values.Sum(list => list.Count);
        sb.AppendLine($"**总记忆数**: {totalCount}");
        sb.AppendLine();

        // 按类型列出记忆
        foreach (var type in Enum.GetValues<MemoryType>())
        {
            if (!memoriesByType.ContainsKey(type) || memoriesByType[type].Count == 0)
            {
                continue;
            }

            var memories = memoriesByType[type];

            sb.AppendLine($"## {GetTypeDisplayName(type)}");
            sb.AppendLine();

            foreach (var memory in memories.OrderBy(m => m.Name))
            {
                var index = MemoryIndex.FromMemory(memory);
                sb.AppendLine(index.ToMarkdownLine());
            }

            sb.AppendLine();
        }

        // 最后更新时间
        sb.AppendLine("---");
        sb.AppendLine($"*最后更新: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// 获取记忆类型的显示名称
    /// </summary>
    private string GetTypeDisplayName(MemoryType type)
    {
        return type switch
        {
            MemoryType.User => "User (用户记忆)",
            MemoryType.Feedback => "Feedback (反馈记忆)",
            MemoryType.Project => "Project (项目记忆)",
            MemoryType.Reference => "Reference (参考记忆)",
            MemoryType.Knowledge => "Knowledge (知识记忆)",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// 从索引内容中提取记忆 ID（简单实现）
    /// </summary>
    private HashSet<Guid> ExtractCoreMemoryIdsFromIndex(string indexContent)
    {
        // 简化实现：假设我们不在索引中直接存储 ID
        // 在实际场景中，可以通过文件路径映射回 ID
        // 这里返回空集合，表示需要完整验证
        return new HashSet<Guid>();
    }
}
