using GeneralAgent.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 工具注册表
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

    public void RegisterMany(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
        {
            Register(tool);
        }
    }

    public ITool? GetTool(string name)
    {
        lock (_lock)
        {
            return _tools.GetValueOrDefault(name);
        }
    }

    public IReadOnlyList<ITool> GetAllTools()
    {
        lock (_lock)
        {
            return _tools.Values.ToList();
        }
    }

    public IReadOnlyList<ITool> GetToolsByNamespace(string namespaceName)
    {
        lock (_lock)
        {
            return _tools.Values
                .Where(t => t.Name.StartsWith($"{namespaceName}:"))
                .ToList();
        }
    }

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

    public void Clear()
    {
        lock (_lock)
        {
            _tools.Clear();
            _logger.LogInformation("清空所有工具");
        }
    }

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
