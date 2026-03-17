using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Parsers;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.Skills.Loaders;

/// <summary>
/// 文件系统技能加载器
/// 从目录加载 Markdown 格式的技能文件
/// </summary>
public class FileSystemSkillLoader : ISkillLoader
{
    private readonly ISkillParser _parser;
    private readonly ILogger<FileSystemSkillLoader> _logger;
    private readonly List<Regex> _ignorePatterns = new();

    public FileSystemSkillLoader(
        ISkillParser parser,
        ILogger<FileSystemSkillLoader> logger)
    {
        _parser = parser;
        _logger = logger;
    }

    public async Task<Result<List<Skill>>> LoadFromDirectoryAsync(string directoryPath)
    {
        try
        {
            // 验证目录是否存在
            if (!Directory.Exists(directoryPath))
            {
                return Result<List<Skill>>.Failure($"目录不存在: {directoryPath}");
            }

            // 加载 .ignore 文件
            await LoadIgnorePatternsAsync(directoryPath);

            var skills = new List<Skill>();

            // 递归加载所有 .md 文件
            await LoadSkillsRecursiveAsync(directoryPath, directoryPath, skills);

            _logger.LogInformation("从目录 {Directory} 加载了 {Count} 个技能", directoryPath, skills.Count);

            return Result<List<Skill>>.Success(skills);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载技能目录失败: {Directory}", directoryPath);
            return Result<List<Skill>>.Failure($"加载技能目录失败: {ex.Message}");
        }
    }

    private async Task LoadSkillsRecursiveAsync(
        string rootPath,
        string currentPath,
        List<Skill> skills)
    {
        // 加载当前目录的所有 .md 文件
        var markdownFiles = Directory.GetFiles(currentPath, "*.md");

        foreach (var filePath in markdownFiles)
        {
            var fileName = Path.GetFileName(filePath);

            // 检查是否被忽略
            if (ShouldIgnoreFile(fileName))
            {
                _logger.LogDebug("跳过被忽略的文件: {File}", fileName);
                continue;
            }

            // 加载技能
            await LoadSingleSkillAsync(rootPath, filePath, skills);
        }

        // 递归处理子目录
        var subdirectories = Directory.GetDirectories(currentPath);
        foreach (var subdirectory in subdirectories)
        {
            await LoadSkillsRecursiveAsync(rootPath, subdirectory, skills);
        }
    }

    private async Task LoadSingleSkillAsync(
        string rootPath,
        string filePath,
        List<Skill> skills)
    {
        try
        {
            // 读取文件内容
            var content = await File.ReadAllTextAsync(filePath);

            // 解析技能
            var parseResult = _parser.Parse(content);

            if (!parseResult.IsSuccess)
            {
                _logger.LogWarning("解析技能文件失败 {File}: {Error}",
                    Path.GetFileName(filePath), parseResult.Error);
                return;
            }

            var skill = parseResult.Value!;

            // 计算命名空间（基于子目录结构）
            var namespaceName = CalculateNamespace(rootPath, filePath);
            if (!string.IsNullOrEmpty(namespaceName))
            {
                skill = skill with { Namespace = namespaceName };
            }

            skills.Add(skill);

            _logger.LogDebug("成功加载技能: {SkillName} (命名空间: {Namespace})",
                skill.Name, skill.Namespace ?? "(无)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载技能文件失败: {File}", Path.GetFileName(filePath));
        }
    }

    private string? CalculateNamespace(string rootPath, string filePath)
    {
        var fileDirectory = Path.GetDirectoryName(filePath);
        if (fileDirectory == null || fileDirectory == rootPath)
        {
            return null;
        }

        // 计算相对路径
        var relativePath = Path.GetRelativePath(rootPath, fileDirectory);

        // 将路径分隔符转换为命名空间分隔符（. 或 :）
        var namespaceName = relativePath
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');

        return namespaceName;
    }

    private async Task LoadIgnorePatternsAsync(string directoryPath)
    {
        _ignorePatterns.Clear();

        var ignoreFilePath = Path.Combine(directoryPath, ".ignore");
        if (!File.Exists(ignoreFilePath))
        {
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(ignoreFilePath);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // 跳过空行和注释
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
                {
                    continue;
                }

                // 将 glob 模式转换为正则表达式
                var pattern = ConvertGlobToRegex(trimmedLine);
                _ignorePatterns.Add(pattern);
            }

            _logger.LogDebug("加载了 {Count} 个忽略模式", _ignorePatterns.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "加载 .ignore 文件失败: {File}", ignoreFilePath);
        }
    }

    private bool ShouldIgnoreFile(string fileName)
    {
        return _ignorePatterns.Any(pattern => pattern.IsMatch(fileName));
    }

    private static Regex ConvertGlobToRegex(string globPattern)
    {
        // 简化的 glob 转正则表达式
        // 支持: * (匹配任意字符), ? (匹配单个字符)
        var regexPattern = "^" + Regex.Escape(globPattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".")
            + "$";

        return new Regex(regexPattern, RegexOptions.IgnoreCase);
    }
}
