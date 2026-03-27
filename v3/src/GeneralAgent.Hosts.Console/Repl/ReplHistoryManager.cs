using Microsoft.Extensions.Logging;

namespace GeneralAgent.Hosts.Console.Repl;

/// <summary>
/// REPL 历史记录管理器
/// 负责加载、保存、搜索和管理命令历史
/// </summary>
public sealed class ReplHistoryManager
{
    private readonly string _historyFilePath;
    private readonly int _maxHistorySize;
    private readonly ILogger<ReplHistoryManager> _logger;
    private readonly object _fileLock = new();

    /// <summary>
    /// 历史记录列表（最新的在最后）
    /// </summary>
    private List<string> _historyItems;

    public ReplHistoryManager(
        string historyFilePath,
        int maxHistorySize = 1000,
        ILogger<ReplHistoryManager>? logger = null)
    {
        _historyFilePath = historyFilePath ?? throw new ArgumentNullException(nameof(historyFilePath));
        _maxHistorySize = maxHistorySize;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReplHistoryManager>.Instance;
        _historyItems = new List<string>();

        // 确保目录存在
        var directory = Path.GetDirectoryName(_historyFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 加载历史记录
    /// </summary>
    public List<string> LoadHistory()
    {
        lock (_fileLock)
        {
            try
            {
                if (!File.Exists(_historyFilePath))
                {
                    _logger.LogDebug("历史文件不存在: {FilePath}", _historyFilePath);
                    _historyItems = new List<string>();
                    return _historyItems;
                }

                var lines = File.ReadAllLines(_historyFilePath);
                _historyItems = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

                // 应用数量限制
                if (_historyItems.Count > _maxHistorySize)
                {
                    _historyItems = _historyItems.TakeLast(_maxHistorySize).ToList();
                    _logger.LogInformation("历史记录已截断到 {MaxSize} 条", _maxHistorySize);
                }

                _logger.LogDebug("已加载 {Count} 条历史记录", _historyItems.Count);
                return _historyItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载历史记录失败");
                _historyItems = new List<string>();
                return _historyItems;
            }
        }
    }

    /// <summary>
    /// 添加历史项
    /// </summary>
    public void AddHistoryItem(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        lock (_fileLock)
        {
            try
            {
                // 避免连续重复的命令
                if (_historyItems.Count > 0 && _historyItems[^1] == command)
                {
                    return;
                }

                _historyItems.Add(command);

                // 应用数量限制
                if (_historyItems.Count > _maxHistorySize)
                {
                    _historyItems.RemoveAt(0);
                }

                // 追加到文件
                File.AppendAllLines(_historyFilePath, new[] { command });

                _logger.LogDebug("已添加历史项: {Command}", command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加历史项失败");
            }
        }
    }

    /// <summary>
    /// 搜索历史记录
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <returns>匹配的历史项列表（最新的在最后）</returns>
    public List<string> SearchHistory(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _historyItems.ToList();
        }

        lock (_fileLock)
        {
            return _historyItems
                .Where(item => item.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// 清空历史记录
    /// </summary>
    public void ClearHistory()
    {
        lock (_fileLock)
        {
            try
            {
                _historyItems.Clear();

                if (File.Exists(_historyFilePath))
                {
                    File.Delete(_historyFilePath);
                }

                _logger.LogInformation("已清空历史记录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清空历史记录失败");
                throw;
            }
        }
    }

    /// <summary>
    /// 导出历史记录到文件
    /// </summary>
    public void ExportHistory(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("输出路径不能为空", nameof(outputPath));
        }

        lock (_fileLock)
        {
            try
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllLines(outputPath, _historyItems);
                _logger.LogInformation("已导出 {Count} 条历史记录到 {OutputPath}", _historyItems.Count, outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出历史记录失败");
                throw;
            }
        }
    }

    /// <summary>
    /// 从文件导入历史记录
    /// </summary>
    public void ImportHistory(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("输入路径不能为空", nameof(inputPath));
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("历史文件不存在", inputPath);
        }

        lock (_fileLock)
        {
            try
            {
                var lines = File.ReadAllLines(inputPath);
                var validLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

                // 合并到现有历史（去重）
                foreach (var line in validLines)
                {
                    if (!_historyItems.Contains(line))
                    {
                        _historyItems.Add(line);
                    }
                }

                // 应用数量限制
                if (_historyItems.Count > _maxHistorySize)
                {
                    _historyItems = _historyItems.TakeLast(_maxHistorySize).ToList();
                }

                // 保存到文件
                File.WriteAllLines(_historyFilePath, _historyItems);

                _logger.LogInformation("已导入 {Count} 条历史记录", validLines.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入历史记录失败");
                throw;
            }
        }
    }

    /// <summary>
    /// 获取历史记录数量
    /// </summary>
    public int Count => _historyItems.Count;

    /// <summary>
    /// 获取所有历史记录（只读）
    /// </summary>
    public IReadOnlyList<string> GetAllHistory()
    {
        lock (_fileLock)
        {
            return _historyItems.ToList();
        }
    }
}
