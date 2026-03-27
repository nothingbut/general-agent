using Microsoft.Extensions.Logging;
using System.Text;

namespace GeneralAgent.Hosts.Console.Repl;

/// <summary>
/// 多行输入处理器
/// 处理多行输入模式的检测、收集和格式化
/// </summary>
public sealed class MultiLineInputHandler
{
    private readonly ILogger<MultiLineInputHandler> _logger;

    // 多行输入标记
    private const string MultiLineMarker = "\"\"\"";

    // 多行提示符
    private const string MultiLinePrompt = "... ";

    public MultiLineInputHandler(ILogger<MultiLineInputHandler>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MultiLineInputHandler>.Instance;
    }

    /// <summary>
    /// 检测是否是多行输入开始标记
    /// </summary>
    public bool IsMultiLineStart(string input)
    {
        return input.Trim() == MultiLineMarker;
    }

    /// <summary>
    /// 检测是否是多行输入结束标记
    /// </summary>
    public bool IsMultiLineEnd(string input)
    {
        // 支持两种结束方式：
        // 1. 再次输入 """
        // 2. 输入空行
        return input.Trim() == MultiLineMarker || string.IsNullOrWhiteSpace(input);
    }

    /// <summary>
    /// 收集多行输入
    /// </summary>
    /// <param name="readLineFunc">读取单行的函数</param>
    /// <returns>完整的多行文本</returns>
    public string CollectMultiLineInput(Func<string, string> readLineFunc)
    {
        var lines = new List<string>();
        var lineCount = 0;

        _logger.LogDebug("开始收集多行输入");

        while (true)
        {
            lineCount++;

            // 读取一行，使用 ... 作为提示符
            var line = readLineFunc(MultiLinePrompt);

            // 检测结束标记
            if (IsMultiLineEnd(line))
            {
                _logger.LogDebug("多行输入结束，共 {LineCount} 行", lineCount - 1);
                break;
            }

            // 添加到行列表
            lines.Add(line);
        }

        // 合并所有行
        var result = string.Join(Environment.NewLine, lines);
        return result;
    }

    /// <summary>
    /// 处理输入（自动检测是否需要多行模式）
    /// </summary>
    /// <param name="initialInput">初始输入</param>
    /// <param name="readLineFunc">读取单行的函数</param>
    /// <returns>处理后的完整输入</returns>
    public string ProcessInput(string initialInput, Func<string, string> readLineFunc)
    {
        if (IsMultiLineStart(initialInput))
        {
            _logger.LogInformation("检测到多行输入模式");
            System.Console.WriteLine("[多行输入模式] 输入内容后，使用空行或 \"\"\" 结束");
            return CollectMultiLineInput(readLineFunc);
        }

        return initialInput;
    }

    /// <summary>
    /// 格式化多行输入显示（用于日志或调试）
    /// </summary>
    public string FormatMultiLineDisplay(string input, int maxLines = 5)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "[空输入]";
        }

        var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length <= maxLines)
        {
            return input;
        }

        // 显示前几行和省略标记
        var preview = string.Join(Environment.NewLine, lines.Take(maxLines));
        return $"{preview}{Environment.NewLine}... [共 {lines.Length} 行]";
    }

    /// <summary>
    /// 获取输入的行数统计
    /// </summary>
    public InputStats GetInputStats(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new InputStats(0, 0, 0);
        }

        var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var nonEmptyLines = lines.Count(l => !string.IsNullOrWhiteSpace(l));
        var totalChars = input.Length;

        return new InputStats(lines.Length, nonEmptyLines, totalChars);
    }
}

/// <summary>
/// 输入统计信息
/// </summary>
public sealed record InputStats(int TotalLines, int NonEmptyLines, int TotalChars)
{
    /// <summary>
    /// 格式化统计信息
    /// </summary>
    public string Format()
    {
        return $"{NonEmptyLines} 行, {TotalChars} 字符";
    }
}
