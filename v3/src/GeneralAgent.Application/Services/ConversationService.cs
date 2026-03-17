using System.Runtime.CompilerServices;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 对话编排服务
///
/// 职责：
/// - 集成 SessionService 和 ILLMClient
/// - 处理 Message ↔ ChatMessage 转换
/// - 支持 SystemPrompt 注入
/// - 管理会话历史
/// - 提供非流式和流式对话方法
/// </summary>
public sealed class ConversationService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILLMClientFactory _llmClientFactory;

    private const string DefaultSystemPrompt = "你是一个有帮助的 AI 助手。";
    private const string DefaultModel = "qwen3.5:0.8b";

    /// <summary>
    /// 初始化 ConversationService
    /// </summary>
    public ConversationService(
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository,
        ILLMClientFactory llmClientFactory)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
    }

    /// <summary>
    /// 发送消息（非流式）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userMessage">用户消息</param>
    /// <param name="providerName">LLM 提供商名称（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>助手响应内容</returns>
    /// <exception cref="InvalidOperationException">会话不存在</exception>
    public async Task<string> SendMessageAsync(
        Guid sessionId,
        string userMessage,
        string? providerName = null,
        CancellationToken ct = default)
    {
        // 1. 验证会话存在
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"会话不存在: {sessionId}");

        // 2. 保存用户消息
        var userMsg = Message.CreateUser(sessionId, userMessage);
        await _messageRepository.CreateAsync(userMsg, ct);

        // 3. 获取会话历史并转换为 ChatMessage
        var history = await _messageRepository.GetBySessionAsync(sessionId, ct);
        var chatMessages = ConvertToChatMessages(history);

        // 4. 调用 LLM
        var client = _llmClientFactory.GetClient(providerName);
        var request = new CompletionRequest
        {
            Model = DefaultModel,
            Messages = chatMessages
        };
        var response = await client.CompleteAsync(request, ct);

        // 5. 保存助手响应
        var assistantMsg = Message.CreateAssistant(sessionId, response.Content);
        await _messageRepository.CreateAsync(assistantMsg, ct);

        return response.Content;
    }

    /// <summary>
    /// 发送消息（流式）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userMessage">用户消息</param>
    /// <param name="providerName">LLM 提供商名称（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>流式响应块</returns>
    /// <exception cref="InvalidOperationException">会话不存在</exception>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        Guid sessionId,
        string userMessage,
        string? providerName = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // 1. 验证会话存在
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"会话不存在: {sessionId}");

        // 2. 保存用户消息
        var userMsg = Message.CreateUser(sessionId, userMessage);
        await _messageRepository.CreateAsync(userMsg, ct);

        // 3. 获取会话历史并转换为 ChatMessage
        var history = await _messageRepository.GetBySessionAsync(sessionId, ct);
        var chatMessages = ConvertToChatMessages(history);

        // 4. 调用 LLM 流式 API
        var client = _llmClientFactory.GetClient(providerName);
        var request = new CompletionRequest
        {
            Model = DefaultModel,
            Messages = chatMessages
        };

        // 5. 流式返回，同时收集完整响应
        var fullResponse = new System.Text.StringBuilder();
        await foreach (var chunk in client.StreamAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                fullResponse.Append(chunk.Delta);
                yield return chunk.Delta;
            }
        }

        // 6. 保存完整的助手响应
        var assistantMsg = Message.CreateAssistant(sessionId, fullResponse.ToString());
        await _messageRepository.CreateAsync(assistantMsg, ct);
    }

    /// <summary>
    /// 转换 Message 列表为 ChatMessage 列表
    /// 如果是首次对话，注入 SystemPrompt
    /// </summary>
    private static List<ChatMessage> ConvertToChatMessages(List<Message> messages)
    {
        var chatMessages = new List<ChatMessage>();

        // 如果没有消息，注入 SystemPrompt
        if (messages.Count == 0)
        {
            chatMessages.Add(new ChatMessage
            {
                Role = "system",
                Content = DefaultSystemPrompt
            });
        }
        else
        {
            // 转换历史消息
            foreach (var msg in messages)
            {
                chatMessages.Add(new ChatMessage
                {
                    Role = msg.Role.ToString().ToLowerInvariant(),
                    Content = msg.Content
                });
            }
        }

        return chatMessages;
    }
}
