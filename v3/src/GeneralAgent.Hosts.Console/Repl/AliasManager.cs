using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Hosts.Console.Repl;

/// <summary>
/// 命令别名管理器
/// 支持自定义命令快捷方式
/// </summary>
public class AliasManager
{
    private readonly string _aliasFilePath;
    private readonly ILogger<AliasManager>? _logger;
    private Dictionary<string, string> _aliases;

    /// <summary>
    /// 别名配置文件版本
    /// </summary>
    private const string ConfigVersion = "1.0";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="aliasFilePath">别名配置文件路径</param>
    /// <param name="logger">日志记录器</param>
    public AliasManager(string aliasFilePath, ILogger<AliasManager>? logger = null)
    {
        _aliasFilePath = aliasFilePath;
        _logger = logger;
        _aliases = new Dictionary<string, string>();

        // 加载别名
        LoadAliases();
    }

    /// <summary>
    /// 加载别名配置
    /// </summary>
    public void LoadAliases()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(_aliasFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 如果文件不存在，使用默认别名
            if (!File.Exists(_aliasFilePath))
            {
                _aliases = GetDefaultAliases();
                SaveAliases();
                _logger?.LogInformation("已创建默认别名配置");
                return;
            }

            // 读取并解析配置文件
            var json = File.ReadAllText(_aliasFilePath);
            var config = JsonSerializer.Deserialize<AliasConfig>(json);

            if (config?.Aliases != null)
            {
                _aliases = new Dictionary<string, string>(config.Aliases);
                _logger?.LogInformation("已加载 {Count} 个别名", _aliases.Count);
            }
            else
            {
                _aliases = GetDefaultAliases();
                _logger?.LogWarning("别名配置格式错误，使用默认别名");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "加载别名配置失败");
            _aliases = GetDefaultAliases();
        }
    }

    /// <summary>
    /// 保存别名配置
    /// </summary>
    public void SaveAliases()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(_aliasFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 创建配置对象
            var config = new AliasConfig
            {
                Aliases = new Dictionary<string, string>(_aliases),
                Version = ConfigVersion
            };

            // 序列化并写入文件
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_aliasFilePath, json);

            _logger?.LogInformation("已保存 {Count} 个别名", _aliases.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存别名配置失败");
            throw;
        }
    }

    /// <summary>
    /// 添加别名
    /// </summary>
    /// <param name="alias">别名</param>
    /// <param name="command">实际命令</param>
    public void AddAlias(string alias, string command)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("别名不能为空", nameof(alias));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("命令不能为空", nameof(command));
        }

        // 检查循环引用
        if (WouldCreateCircularReference(alias, command))
        {
            throw new InvalidOperationException($"添加别名 '{alias}' -> '{command}' 会创建循环引用");
        }

        _aliases[alias] = command;
        _logger?.LogInformation("已添加别名: {Alias} -> {Command}", alias, command);
    }

    /// <summary>
    /// 移除别名
    /// </summary>
    /// <param name="alias">别名</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("别名不能为空", nameof(alias));
        }

        var removed = _aliases.Remove(alias);
        if (removed)
        {
            _logger?.LogInformation("已移除别名: {Alias}", alias);
        }

        return removed;
    }

    /// <summary>
    /// 解析别名
    /// </summary>
    /// <param name="input">用户输入</param>
    /// <returns>解析后的命令</returns>
    public string ResolveAlias(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // 如果不是命令，直接返回
        if (!input.StartsWith('/'))
        {
            return input;
        }

        // 分离命令和参数
        var parts = input.TrimStart('/').Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return input;
        }

        var command = parts[0];
        var remainingArgs = parts.Length > 1 ? parts[1] : string.Empty;

        // 解析别名（最多 10 次防止循环）
        var resolved = ResolveAliasRecursive(command, maxDepth: 10);

        // 重新组合命令
        return string.IsNullOrEmpty(remainingArgs)
            ? $"/{resolved}"
            : $"/{resolved} {remainingArgs}";
    }

    /// <summary>
    /// 递归解析别名
    /// </summary>
    private string ResolveAliasRecursive(string command, int maxDepth)
    {
        if (maxDepth <= 0)
        {
            _logger?.LogWarning("别名解析达到最大深度，可能存在循环引用");
            return command;
        }

        if (_aliases.TryGetValue(command, out var resolvedCommand))
        {
            _logger?.LogDebug("解析别名: {Alias} -> {Command}", command, resolvedCommand);
            return ResolveAliasRecursive(resolvedCommand, maxDepth - 1);
        }

        return command;
    }

    /// <summary>
    /// 获取所有别名
    /// </summary>
    /// <returns>别名字典（只读副本）</returns>
    public IReadOnlyDictionary<string, string> GetAllAliases()
    {
        return new Dictionary<string, string>(_aliases);
    }

    /// <summary>
    /// 检查别名是否存在
    /// </summary>
    public bool HasAlias(string alias)
    {
        return _aliases.ContainsKey(alias);
    }

    /// <summary>
    /// 检查是否会创建循环引用
    /// </summary>
    private bool WouldCreateCircularReference(string newAlias, string newCommand)
    {
        // 构建临时别名集合
        var tempAliases = new Dictionary<string, string>(_aliases)
        {
            [newAlias] = newCommand
        };

        // 尝试解析新别名
        var visited = new HashSet<string>();
        var current = newCommand;

        for (int i = 0; i < 100; i++) // 最多检查 100 层
        {
            if (visited.Contains(current))
            {
                return true; // 检测到循环
            }

            visited.Add(current);

            if (!tempAliases.TryGetValue(current, out var next))
            {
                return false; // 没有更多别名，安全
            }

            current = next;
        }

        return true; // 超过最大深度，认为是循环
    }

    /// <summary>
    /// 获取默认别名
    /// </summary>
    private static Dictionary<string, string> GetDefaultAliases()
    {
        return new Dictionary<string, string>
        {
            ["n"] = "new",
            ["ls"] = "list",
            ["s"] = "session",
            ["del"] = "delete",
            ["q"] = "quit",
            ["h"] = "help"
        };
    }

    /// <summary>
    /// 别名配置类
    /// </summary>
    private class AliasConfig
    {
        public Dictionary<string, string> Aliases { get; set; } = new();
        public string Version { get; set; } = string.Empty;
    }
}
