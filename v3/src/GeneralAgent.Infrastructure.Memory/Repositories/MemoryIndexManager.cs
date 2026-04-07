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
        // 增量添加：直接在索引文件中插入新条目
        if (!File.Exists(_indexFilePath))
        {
            // 索引文件不存在，重建索引
            await RebuildIndexAsync(cancellationToken);
            return;
        }

        try
        {
            // 1. 读取现有索引
            var indexContent = await File.ReadAllTextAsync(_indexFilePath, Encoding.UTF8, cancellationToken);
            var lines = indexContent.Split('\n').ToList();

            // 2. 找到对应类型的部分
            var typeHeader = $"## {GetTypeDisplayName(memory.Type)}";
            var typeIndex = lines.FindIndex(l => l.StartsWith(typeHeader));

            if (typeIndex == -1)
            {
                // 该类型部分不存在，需要创建
                typeIndex = InsertTypeSectionHeader(lines, memory.Type);
            }

            // 3. 找到插入位置（按名称排序）
            var insertIndex = FindInsertPosition(lines, typeIndex, memory.Name);

            // 4. 插入新条目
            var newEntry = MemoryIndex.FromMemory(memory).ToMarkdownLine();
            lines.Insert(insertIndex, newEntry);

            // 5. 更新总记忆数
            UpdateTotalCount(lines, +1);

            // 6. 更新时间戳
            UpdateTimestamp(lines);

            // 7. 写回文件
            await File.WriteAllTextAsync(_indexFilePath, string.Join('\n', lines), Encoding.UTF8, cancellationToken);

            _logger.LogDebug("✅ 增量添加记忆到索引: {Name}", memory.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "增量添加失败，回退到重建索引");
            await RebuildIndexAsync(cancellationToken);
        }
    }

    public async Task RemoveFromIndexAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        // 增量删除：直接从索引文件中移除条目
        if (!File.Exists(_indexFilePath))
        {
            _logger.LogWarning("索引文件不存在，跳过删除");
            return;
        }

        try
        {
            // 1. 读取现有索引
            var indexContent = await File.ReadAllTextAsync(_indexFilePath, Encoding.UTF8, cancellationToken);
            var lines = indexContent.Split('\n').ToList();

            // 2. 查找包含该 ID 的行
            var lineIndex = lines.FindIndex(l => l.Contains($"<!-- id:{memoryId} -->"));

            if (lineIndex == -1)
            {
                _logger.LogWarning("未在索引中找到记忆 ID: {Id}，可能已被删除", memoryId);
                return;
            }

            // 3. 删除该行
            lines.RemoveAt(lineIndex);

            // 4. 检查是否需要清理空的类型部分
            CleanupEmptyTypeSections(lines);

            // 5. 更新总记忆数
            UpdateTotalCount(lines, -1);

            // 6. 更新时间戳
            UpdateTimestamp(lines);

            // 7. 写回文件
            await File.WriteAllTextAsync(_indexFilePath, string.Join('\n', lines), Encoding.UTF8, cancellationToken);

            _logger.LogDebug("✅ 增量删除索引中的记忆: {Id}", memoryId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "增量删除失败，回退到重建索引");
            await RebuildIndexAsync(cancellationToken);
        }
    }

    public async Task UpdateInIndexAsync(Core.Models.Memory memory, CancellationToken cancellationToken = default)
    {
        // 增量更新：先删除旧条目，再添加新条目
        // 这样可以处理记忆类型或名称变化的情况
        if (!File.Exists(_indexFilePath))
        {
            // 索引文件不存在，重建索引
            await RebuildIndexAsync(cancellationToken);
            return;
        }

        try
        {
            // 1. 读取现有索引
            var indexContent = await File.ReadAllTextAsync(_indexFilePath, Encoding.UTF8, cancellationToken);
            var lines = indexContent.Split('\n').ToList();

            // 2. 查找并删除旧条目
            var oldLineIndex = lines.FindIndex(l => l.Contains($"<!-- id:{memory.Id} -->"));

            if (oldLineIndex >= 0)
            {
                lines.RemoveAt(oldLineIndex);
            }

            // 3. 找到新的插入位置（可能类型已变化）
            var typeHeader = $"## {GetTypeDisplayName(memory.Type)}";
            var typeIndex = lines.FindIndex(l => l.StartsWith(typeHeader));

            if (typeIndex == -1)
            {
                // 该类型部分不存在，需要创建
                typeIndex = InsertTypeSectionHeader(lines, memory.Type);
            }

            // 4. 找到插入位置（按名称排序）
            var insertIndex = FindInsertPosition(lines, typeIndex, memory.Name);

            // 5. 插入更新后的条目
            var newEntry = MemoryIndex.FromMemory(memory).ToMarkdownLine();
            lines.Insert(insertIndex, newEntry);

            // 6. 清理可能的空类型部分（如果类型变化了）
            CleanupEmptyTypeSections(lines);

            // 7. 更新时间戳
            UpdateTimestamp(lines);

            // 8. 写回文件
            await File.WriteAllTextAsync(_indexFilePath, string.Join('\n', lines), Encoding.UTF8, cancellationToken);

            _logger.LogDebug("✅ 增量更新索引中的记忆: {Name}", memory.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "增量更新失败，回退到重建索引");
            await RebuildIndexAsync(cancellationToken);
        }
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

    /// <summary>
    /// 在索引文件中插入类型部分标题
    /// </summary>
    private int InsertTypeSectionHeader(List<string> lines, MemoryType type)
    {
        // 找到统计信息行之后的位置（跳过空行）
        var insertIndex = lines.FindIndex(l => l.StartsWith("**总记忆数**"));
        if (insertIndex == -1)
        {
            // 如果找不到，插入到文件末尾的时间戳之前
            insertIndex = lines.FindLastIndex(l => l.StartsWith("---"));
            if (insertIndex == -1)
            {
                insertIndex = lines.Count;
            }
        }
        else
        {
            // 跳过统计信息和空行
            insertIndex += 2;
        }

        // 找到合适的位置插入（按类型枚举值排序）
        var typeValue = (int)type;
        for (int i = insertIndex; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("## "))
            {
                // 解析类型
                var existingType = ParseTypeFromHeader(lines[i]);
                if (existingType.HasValue && (int)existingType.Value > typeValue)
                {
                    // 在此之前插入
                    lines.Insert(i, "");
                    lines.Insert(i, "");
                    lines.Insert(i, GetTypeDisplayName(type));
                    lines.Insert(i, "##");
                    return i;
                }
            }
            else if (lines[i].StartsWith("---"))
            {
                // 到达文件末尾标记，在此之前插入
                lines.Insert(i, "");
                lines.Insert(i, "");
                lines.Insert(i, GetTypeDisplayName(type));
                lines.Insert(i, "##");
                return i;
            }
        }

        // 在文件末尾插入
        lines.Add("##");
        lines.Add(GetTypeDisplayName(type));
        lines.Add("");
        lines.Add("");
        return lines.Count - 4;
    }

    /// <summary>
    /// 找到插入条目的位置（按名称排序）
    /// </summary>
    private int FindInsertPosition(List<string> lines, int typeHeaderIndex, string memoryName)
    {
        // 从类型标题之后开始查找
        var startIndex = typeHeaderIndex + 2; // 跳过标题和空行

        for (int i = startIndex; i < lines.Count; i++)
        {
            var line = lines[i].Trim();

            // 遇到空行或下一个类型标题，说明到达了该类型部分的末尾
            if (string.IsNullOrEmpty(line) || line.StartsWith("##") || line.StartsWith("---"))
            {
                return i;
            }

            // 解析现有条目的名称
            if (line.StartsWith("- ["))
            {
                var existingName = ExtractNameFromLine(line);
                if (string.Compare(memoryName, existingName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // 找到了插入位置（按字母顺序）
                    return i;
                }
            }
        }

        // 在末尾插入
        return lines.Count;
    }

    /// <summary>
    /// 从索引行中提取记忆名称
    /// 格式: - [Name](filepath.md) — description
    /// </summary>
    private string ExtractNameFromLine(string line)
    {
        var startIndex = line.IndexOf('[');
        var endIndex = line.IndexOf(']');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return line.Substring(startIndex + 1, endIndex - startIndex - 1);
        }

        return string.Empty;
    }

    /// <summary>
    /// 从类型标题行解析 MemoryType
    /// </summary>
    private MemoryType? ParseTypeFromHeader(string headerLine)
    {
        foreach (var type in Enum.GetValues<MemoryType>())
        {
            if (headerLine.Contains(type.ToString()))
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// 更新总记忆数统计
    /// </summary>
    private void UpdateTotalCount(List<string> lines, int delta)
    {
        var totalCountIndex = lines.FindIndex(l => l.StartsWith("**总记忆数**"));
        if (totalCountIndex >= 0)
        {
            var line = lines[totalCountIndex];
            // 解析当前数量
            var match = System.Text.RegularExpressions.Regex.Match(line, @"\*\*总记忆数\*\*:\s*(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var currentCount))
            {
                var newCount = currentCount + delta;
                lines[totalCountIndex] = $"**总记忆数**: {newCount}";
            }
        }
    }

    /// <summary>
    /// 更新时间戳
    /// </summary>
    private void UpdateTimestamp(List<string> lines)
    {
        // 查找时间戳行
        var timestampIndex = lines.FindLastIndex(l => l.StartsWith("*最后更新:"));
        if (timestampIndex >= 0)
        {
            lines[timestampIndex] = $"*最后更新: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*";
        }
    }

    /// <summary>
    /// 清理空的类型部分
    /// 如果某个类型下没有记忆条目，删除该类型的标题
    /// </summary>
    private void CleanupEmptyTypeSections(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("## "))
            {
                // 找到类型标题，检查后面是否有记忆条目
                var hasEntries = false;

                for (int j = i + 1; j < lines.Count; j++)
                {
                    var line = lines[j].Trim();

                    if (string.IsNullOrEmpty(line))
                    {
                        continue; // 跳过空行
                    }

                    if (line.StartsWith("##") || line.StartsWith("---"))
                    {
                        // 遇到下一个部分或分隔线，停止检查
                        break;
                    }

                    if (line.StartsWith("- ["))
                    {
                        // 找到记忆条目
                        hasEntries = true;
                        break;
                    }
                }

                if (!hasEntries)
                {
                    // 删除空的类型部分（标题 + 后续的空行）
                    var endIndex = i + 1;
                    while (endIndex < lines.Count && string.IsNullOrWhiteSpace(lines[endIndex]))
                    {
                        endIndex++;
                    }

                    // 删除从标题到最后一个空行
                    lines.RemoveRange(i, endIndex - i);

                    // 重新检查当前位置（因为删除了行）
                    i--;
                }
            }
        }
    }
}
