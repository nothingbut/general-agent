using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 工具注册表
/// 线程安全的工具注册、查找、列举和管理
/// 所有工具（Skill、MCP、RAG 等）都通过此注册表进行注册和查找
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;
    private readonly object _lock = new();

    public ToolRegistry(ILogger<ToolRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册单个工具
    /// 如果工具已存在，将被覆盖
    /// </summary>
    /// <param name="tool">要注册的工具</param>
    /// <exception cref="ArgumentNullException">当 tool 为 null 时抛出</exception>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        lock (_lock)
        {
            if (_tools.ContainsKey(tool.Name))
            {
                _logger.LogWarning("工具 {ToolName} 已存在，将被覆盖", tool.Name);
            }

            _tools[tool.Name] = tool;
            _logger.LogDebug("注册工具: {ToolName} - {Description}",
                tool.Name, tool.Description);
        }
    }

    /// <summary>
    /// 批量注册工具
    /// </summary>
    /// <param name="tools">要注册的工具集合</param>
    public void RegisterMany(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    /// <summary>
    /// 获取工具
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <returns>工具实例，不存在时返回 null</returns>
    public ITool? GetTool(string name)
    {
        lock (_lock)
        {
            return _tools.GetValueOrDefault(name);
        }
    }

    /// <summary>
    /// 获取所有已注册的工具
    /// </summary>
    /// <returns>只读的工具列表</returns>
    public IReadOnlyList<ITool> GetAllTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList();
        }
    }

    /// <summary>
    /// 获取指定命名空间下的所有工具
    /// 工具名称格式: namespace:tool_name
    /// </summary>
    /// <param name="namespaceName">命名空间名称</param>
    /// <returns>只读的工具列表</returns>
    public IReadOnlyList<ITool> GetToolsByNamespace(string namespaceName)
    {
        lock (_lock)
        {
            return _tools.Values
                .Where(t => t.Name.StartsWith($"{namespaceName}:"))
                .ToList();
        }
    }

    /// <summary>
    /// 取消注册工具
    /// </summary>
    /// <param name="name">工具名称</param>
    /// <returns>成功取消注册返回 true，工具不存在返回 false</returns>
    public bool Unregister(string name)
    {
        lock (_lock)
        {
            if (_tools.Remove(name))
            {
                _logger.LogDebug("取消注册工具: {ToolName}", name);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 清空所有已注册的工具
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _tools.Clear();
            _logger.LogInformation("清空所有工具");
        }
    }

    /// <summary>
    /// 获取已注册的工具总数
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _tools.Count;
            }
        }
    }
}
