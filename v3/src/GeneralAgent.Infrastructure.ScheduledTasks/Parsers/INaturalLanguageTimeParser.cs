namespace GeneralAgent.Infrastructure.ScheduledTasks.Parsers;

/// <summary>
/// 自然语言时间解析器接口
/// </summary>
public interface INaturalLanguageTimeParser
{
    /// <summary>
    /// 解析自然语言时间描述为 cron 表达式
    /// </summary>
    /// <param name="naturalLanguage">自然语言描述（如："每天早上9点"、"每周五下午5点"）</param>
    /// <returns>对应的 cron 表达式（如："0 9 * * *"）</returns>
    string ParseToCron(string naturalLanguage);

    /// <summary>
    /// 验证自然语言是否可解析
    /// </summary>
    /// <param name="naturalLanguage">自然语言描述</param>
    /// <returns>true 表示可解析，false 表示不可解析</returns>
    bool CanParse(string naturalLanguage);

    /// <summary>
    /// 获取支持的自然语言模式示例
    /// </summary>
    /// <returns>支持的模式列表</returns>
    List<string> GetSupportedPatterns();
}
