using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
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

    public MemoryRepository(
        IOptions<MemoryOptions> options,
        ILogger<MemoryRepository> logger,
        IVectorRepository? vectorRepository = null,
        IEmbeddingClient? embeddingClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _rootPath = _options.RootDirectory;
        _vectorRepository = vectorRepository;
        _embeddingClient = embeddingClient;

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
        var allMemories = await GetAllAsync(cancellationToken);
        return allMemories.FirstOrDefault(m => m.Id == id);
    }

    public async Task<List<Core.Models.Memory>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idSet = ids.ToHashSet();
        var allMemories = await GetAllAsync(cancellationToken);
        return allMemories.Where(m => idSet.Contains(m.Id)).ToList();
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
        var memories = type.HasValue
            ? await GetByTypeAsync(type.Value, cancellationToken)
            : await GetAllAsync(cancellationToken);

        var searchKeyword = keyword.ToLower();

        return memories.Where(m =>
            m.Name.ToLower().Contains(searchKeyword) ||
            m.Description.ToLower().Contains(searchKeyword) ||
            m.Content.ToLower().Contains(searchKeyword) ||
            m.Tags.Any(t => t.ToLower().Contains(searchKeyword))
        ).ToList();
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
        var deleted = false;

        // 需要先找到文件
        var allFiles = Directory.GetFiles(_rootPath, "*.md", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains($"id: {id}"))
                {
                    File.Delete(file);
                    _logger.LogInformation("删除记忆文件: {File}", file);
                    deleted = true;
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查或删除文件失败: {File}", file);
            }
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
}
