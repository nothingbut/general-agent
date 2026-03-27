using GeneralAgent.Application.Services;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Hosts.Console.Repl;

/// <summary>
/// REPL 自动补全处理器
/// 实现 ReadLine 的 IAutoCompleteHandler 接口
/// </summary>
public sealed class AutoCompletionHandler : IAutoCompleteHandler
{
    private readonly SessionService _sessionService;
    private readonly SkillService _skillService;
    private readonly ILogger<AutoCompletionHandler> _logger;

    // 缓存
    private List<string>? _cachedSessionIds;
    private DateTime _sessionCacheTime = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5);

    // 内置命令列表
    private static readonly string[] BuiltInCommands = new[]
    {
        "/help",
        "/exit",
        "/quit",
        "/new",
        "/list",
        "/session",
        "/delete",
        "/switch",
        "/provider",
        "/history",
        "/skills",
        "/skill",
        "/clear"
    };

    public AutoCompletionHandler(
        SessionService sessionService,
        SkillService skillService,
        ILogger<AutoCompletionHandler> logger)
    {
        _sessionService = sessionService;
        _skillService = skillService;
        _logger = logger;
    }

    /// <summary>
    /// ReadLine IAutoCompleteHandler 接口实现
    /// 分隔符（用于确定补全边界）
    /// </summary>
    public char[] Separators { get; set; } = new[] { ' ', '\t' };

    /// <summary>
    /// ReadLine IAutoCompleteHandler 接口实现
    /// 获取补全建议
    /// </summary>
    /// <param name="text">当前输入的文本</param>
    /// <param name="index">光标位置</param>
    /// <returns>补全建议列表</returns>
    public string[] GetSuggestions(string text, int index)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            // 分析当前输入上下文
            var context = AnalyzeContext(text, index);

            return context.Type switch
            {
                CompletionType.Command => CompleteCommand(context.Prefix),
                CompletionType.SessionId => CompleteSessionId(context.Prefix),
                CompletionType.SkillName => CompleteSkillName(context.Prefix),
                CompletionType.FilePath => CompleteFilePath(context.Prefix),
                _ => Array.Empty<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取补全建议时发生错误");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 分析当前输入上下文
    /// </summary>
    private CompletionContext AnalyzeContext(string text, int index)
    {
        // 获取光标前的文本
        var beforeCursor = text.Substring(0, Math.Min(index, text.Length));
        var parts = beforeCursor.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return new CompletionContext(CompletionType.None, "");
        }

        // 获取当前正在输入的部分
        var currentPart = parts[^1];

        // 判断补全类型
        if (parts.Length == 1 && beforeCursor.StartsWith('/'))
        {
            // 命令补全
            return new CompletionContext(CompletionType.Command, currentPart);
        }

        if (parts.Length >= 2)
        {
            var command = parts[0].ToLower();

            // 会话 ID 补全
            if (command is "/session" or "/delete")
            {
                return new CompletionContext(CompletionType.SessionId, currentPart);
            }

            // 技能名称补全
            if (command == "/skill")
            {
                return new CompletionContext(CompletionType.SkillName, currentPart);
            }

            // 文件路径补全（检测 --output 等参数）
            if (parts.Length >= 3 && parts[^2].StartsWith("--"))
            {
                var paramName = parts[^2].ToLower();
                if (paramName is "--output" or "-o" or "--file" or "-f")
                {
                    return new CompletionContext(CompletionType.FilePath, currentPart);
                }
            }
        }

        // 技能调用补全（@namespace:name）
        if (currentPart.StartsWith('@'))
        {
            return new CompletionContext(CompletionType.SkillName, currentPart);
        }

        return new CompletionContext(CompletionType.None, currentPart);
    }

    /// <summary>
    /// 补全命令
    /// </summary>
    private string[] CompleteCommand(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return BuiltInCommands;
        }

        var matches = BuiltInCommands
            .Where(cmd => cmd.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(cmd => cmd)
            .ToArray();

        _logger.LogDebug("命令补全: '{Prefix}' -> {Count} 个结果", prefix, matches.Length);
        return matches;
    }

    /// <summary>
    /// 补全会话 ID
    /// </summary>
    private string[] CompleteSessionId(string prefix)
    {
        try
        {
            // 使用缓存
            if (_cachedSessionIds == null || DateTime.Now - _sessionCacheTime > _cacheExpiry)
            {
                var pagedResult = _sessionService.ListSessionsAsync(limit: 100, offset: 0).GetAwaiter().GetResult();
                _cachedSessionIds = pagedResult.Items
                    .Select(s => s.Id.ToString()[..8]) // 短 ID（前 8 个字符）
                    .ToList();
                _sessionCacheTime = DateTime.Now;
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                return _cachedSessionIds.Take(10).ToArray(); // 最多显示 10 个
            }

            var matches = _cachedSessionIds
                .Where(id => id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToArray();

            _logger.LogDebug("会话 ID 补全: '{Prefix}' -> {Count} 个结果", prefix, matches.Length);
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取会话列表失败");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 补全技能名称
    /// </summary>
    private string[] CompleteSkillName(string prefix)
    {
        try
        {
            var skills = _skillService.GetAllSkills();

            // 移除 @ 前缀（如果有）
            var searchPrefix = prefix.TrimStart('@');

            if (string.IsNullOrWhiteSpace(searchPrefix))
            {
                // 返回所有技能的完整名称
                return skills
                    .Select(s => s.FullName)
                    .OrderBy(name => name)
                    .Take(10)
                    .ToArray();
            }

            // 匹配完整名称或简短名称
            var matches = skills
                .Where(s =>
                    s.FullName.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(s => prefix.StartsWith('@') ? $"@{s.FullName}" : s.FullName)
                .OrderBy(name => name)
                .Take(10)
                .ToArray();

            _logger.LogDebug("技能名称补全: '{Prefix}' -> {Count} 个结果", prefix, matches.Length);
            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取技能列表失败");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 补全文件路径
    /// </summary>
    private string[] CompleteFilePath(string prefix)
    {
        try
        {
            // 展开 ~ 为用户主目录
            var expandedPrefix = prefix.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), prefix[1..].TrimStart('/'))
                : prefix;

            // 获取目录和文件名部分
            var directory = Path.GetDirectoryName(expandedPrefix) ?? ".";
            var fileName = Path.GetFileName(expandedPrefix);

            // 如果目录不存在，返回空
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            // 获取匹配的文件和目录
            var entries = new List<string>();

            // 添加目录
            if (Directory.Exists(directory))
            {
                var directories = Directory.GetDirectories(directory, $"{fileName}*")
                    .Select(d => Path.GetFileName(d) + "/")
                    .Take(5);
                entries.AddRange(directories);

                // 添加文件
                var files = Directory.GetFiles(directory, $"{fileName}*")
                    .Select(f => Path.GetFileName(f))
                    .Take(5);
                entries.AddRange(files);
            }

            _logger.LogDebug("文件路径补全: '{Prefix}' -> {Count} 个结果", prefix, entries.Count);
            return entries.Take(10).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "补全文件路径失败");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache()
    {
        _cachedSessionIds = null;
        _sessionCacheTime = DateTime.MinValue;
    }
}

/// <summary>
/// 补全上下文
/// </summary>
internal record CompletionContext(CompletionType Type, string Prefix);

/// <summary>
/// 补全类型
/// </summary>
internal enum CompletionType
{
    None,
    Command,
    SessionId,
    SkillName,
    FilePath
}
