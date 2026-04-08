using GeneralAgent.Infrastructure.SkillExtraction.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能写入器实现 - 将技能定义保存到文件系统
/// </summary>
public sealed class SkillWriter : ISkillWriter
{
    private readonly SkillExtractionOptions _options;
    private readonly ILogger<SkillWriter> _logger;

    public SkillWriter(
        IOptions<SkillExtractionOptions> options,
        ILogger<SkillWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveSkillAsync(
        string @namespace,
        string name,
        string content,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("保存技能: {@namespace}:{name}", @namespace, name);

        // 验证参数
        ValidateNamespace(@namespace);
        ValidateName(name);

        // 获取文件路径
        var filePath = GetSkillPath(@namespace, name);

        // 检查文件是否已存在
        if (File.Exists(filePath) && !_options.OverwriteExisting)
        {
            throw new InvalidOperationException(
                $"技能文件已存在: {filePath}。请先删除或设置 OverwriteExisting = true");
        }

        // 创建命名空间目录
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
        {
            if (_options.AutoCreateNamespaceDirectory)
            {
                _logger.LogDebug("创建命名空间目录: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }
            else
            {
                throw new DirectoryNotFoundException(
                    $"命名空间目录不存在: {directory}。请先创建目录或设置 AutoCreateNamespaceDirectory = true");
            }
        }

        // 写入文件
        await File.WriteAllTextAsync(filePath, content, cancellationToken);

        _logger.LogInformation("技能文件已保存: {FilePath}", filePath);

        return filePath;
    }

    public async Task UpdateSkillAsync(
        string skillPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("更新技能文件: {FilePath}", skillPath);

        if (!File.Exists(skillPath))
        {
            throw new FileNotFoundException($"技能文件不存在: {skillPath}");
        }

        // 写入文件
        await File.WriteAllTextAsync(skillPath, content, cancellationToken);

        _logger.LogInformation("技能文件已更新: {FilePath}", skillPath);
    }

    public Task<bool> DeleteSkillAsync(
        string skillPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("删除技能文件: {FilePath}", skillPath);

        if (!File.Exists(skillPath))
        {
            _logger.LogWarning("技能文件不存在: {FilePath}", skillPath);
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(skillPath);
            _logger.LogInformation("技能文件已删除: {FilePath}", skillPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除技能文件失败: {FilePath}", skillPath);
            throw;
        }
    }

    public Task<bool> ExistsAsync(
        string @namespace,
        string name,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetSkillPath(@namespace, name);
        return Task.FromResult(File.Exists(filePath));
    }

    public string GetSkillPath(string @namespace, string name)
    {
        // 构建文件路径: skills/{namespace}/{name}.md
        var namespaceDir = Path.Combine(_options.SkillsDirectory, @namespace);
        var fileName = $"{name}.md";
        return Path.Combine(namespaceDir, fileName);
    }

    private static void ValidateNamespace(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            throw new ArgumentException("命名空间不能为空", nameof(@namespace));
        }

        // 检查非法字符
        var invalidChars = Path.GetInvalidFileNameChars();
        if (@namespace.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException(
                $"命名空间包含非法字符: {@namespace}", nameof(@namespace));
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("技能名称不能为空", nameof(name));
        }

        // 检查非法字符
        var invalidChars = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException(
                $"技能名称包含非法字符: {name}", nameof(name));
        }
    }
}
