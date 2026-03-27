using GeneralAgent.Core.Models;

namespace GeneralAgent.Infrastructure.Compression.Services;

/// <summary>
/// Token 计数器接口
/// </summary>
public interface ITokenCounter
{
    /// <summary>
    /// 计算文本的 Token 数量
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <returns>Token 数量</returns>
    int CountTokens(string text);

    /// <summary>
    /// 计算单条消息的 Token 数量
    /// </summary>
    /// <param name="message">消息对象</param>
    /// <returns>Token 数量</returns>
    int CountMessageTokens(Message message);

    /// <summary>
    /// 计算消息列表的总 Token 数量
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <returns>总 Token 数量</returns>
    int CountMessagesTokens(List<Message> messages);

    /// <summary>
    /// 截断文本到指定的 Token 限制
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <param name="maxTokens">最大 Token 数</param>
    /// <returns>截断后的文本</returns>
    string TruncateToTokenLimit(string text, int maxTokens);
}
