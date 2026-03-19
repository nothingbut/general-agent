using System.Runtime.CompilerServices;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 对话编排服务
///
/// 职责：
/// - 显式技能调用 (@skill, /skill) → ToolExecutor
/// - 隐式工具调用 → ToolCallingOrchestrator
/// - 处理 Message ↔ ChatMessage 转换
/// - 管理会话历史
/// - 提供非流式和流式对话方法
/// </summary>
public sealed class ConversationService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly ILLMClientFactory _llmClientFactory;
    private readonly ToolCallingOrchestrator _orchestrator;
    private readonly ToolExecutor _toolExecutor;
    private readonly ILogger<ConversationService> _logger;

    private const string DefaultSystemPrompt = "你是一个有帮助的 AI 助手。";

    /// <summary>
    /// 初始化 ConversationService
    /// </summary>
    public ConversationService(
        ISessionRepository sessionRepository,
        IMessageRepository messageRepository,
        ILLMClientFactory llmClientFactory,
        ToolCallingOrchestrator orchestrator,
        ToolExecutor toolExecutor,
        ILogger<ConversationService> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _messageRepository = messageRepository ?? throw new ArgumentNullException(nameof(messageRepository));
        _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        string responseContent;

        // 3. 检查是否是显式技能调用 (@skill 或 /skill)
        if (SkillCallParser.TryParse(userMessage, out var skillCall) && skillCall != null)
        {
            _logger.LogDebug("检测到显式技能调用: {SkillName}", skillCall.SkillName);

            // 显式调用：直接执行工具
            var toolCallObj = new ToolCall
            {
                Id = Guid.NewGuid().ToString(),
                ToolName = skillCall.SkillName,
                Arguments = skillCall.Arguments
            };

            var context = new ToolExecutionContext
            {
                SessionId = sessionId,
                ProviderName = providerName
            };

            var result = await _toolExecutor.ExecuteAsync(toolCallObj, context, timeout: null, ct);

            responseContent = result.IsSuccess
                ? result.Content
                : $"❌ {result.ErrorMessage}";
        }
        else
        {
            _logger.LogDebug("普通消息，通过 Orchestrator 处理");

            // 4. 隐式调用：通过 Orchestrator 处理
            var history = await GetChatHistoryAsync(sessionId, ct);
            var conversationResult = await _orchestrator.ExecuteAsync(sessionId, history, providerName, ct);

            // 5. 保存对话历史（工具调用和结果）
            await SaveConversationHistoryAsync(sessionId, conversationResult.Messages, ct);

            responseContent = conversationResult.FinalResponse;
        }

        // 6. 保存最终响应
        var assistantMsg = Message.CreateAssistant(sessionId, responseContent);
        await _messageRepository.CreateAsync(assistantMsg, ct);

        return responseContent;
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
    /// <remarks>
    /// 注意：流式模式下，工具调用不支持流式返回，会一次性返回结果
    /// </remarks>
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

        string fullResponse;

        // 3. 检查是否是显式技能调用
        if (SkillCallParser.TryParse(userMessage, out var skillCall) && skillCall != null)
        {
            _logger.LogDebug("流式模式：检测到显式技能调用: {SkillName}", skillCall.SkillName);

            // 显式调用：直接执行工具（非流式）
            var toolCallObj = new ToolCall
            {
                Id = Guid.NewGuid().ToString(),
                ToolName = skillCall.SkillName,
                Arguments = skillCall.Arguments
            };

            var context = new ToolExecutionContext
            {
                SessionId = sessionId,
                ProviderName = providerName
            };

            var result = await _toolExecutor.ExecuteAsync(toolCallObj, context, timeout: null, ct);

            fullResponse = result.IsSuccess
                ? result.Content
                : $"❌ {result.ErrorMessage}";

            // 一次性返回技能结果
            yield return fullResponse;
        }
        else
        {
            _logger.LogDebug("流式模式：普通消息，通过 Orchestrator 处理（非流式）");

            // 注意：目前 Orchestrator 不支持流式，直接返回完整结果
            var history = await GetChatHistoryAsync(sessionId, ct);
            var conversationResult = await _orchestrator.ExecuteAsync(sessionId, history, providerName, ct);

            // 保存对话历史
            await SaveConversationHistoryAsync(sessionId, conversationResult.Messages, ct);

            fullResponse = conversationResult.FinalResponse;

            // 一次性返回完整响应
            yield return fullResponse;
        }

        // 4. 保存完整的助手响应
        var assistantMsg = Message.CreateAssistant(sessionId, fullResponse);
        await _messageRepository.CreateAsync(assistantMsg, ct);
    }

    /// <summary>
    /// 获取会话的聊天历史并转换为 ChatMessage 列表
    /// </summary>
    private async Task<List<ChatMessage>> GetChatHistoryAsync(Guid sessionId, CancellationToken ct)
    {
        var messages = await _messageRepository.GetBySessionAsync(sessionId, ct);
        var chatMessages = new List<ChatMessage>();

        // 如果没有历史消息，注入 SystemPrompt
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

    /// <summary>
    /// 保存对话历史中的新消息（工具调用和结果）
    /// </summary>
    /// <remarks>
    /// 只保存 Orchestrator 返回的新消息，不保存已存在的历史消息
    /// </remarks>
    private async Task SaveConversationHistoryAsync(
        Guid sessionId,
        List<ChatMessage> messages,
        CancellationToken ct)
    {
        // 获取当前已保存的消息数量
        var existingMessages = await _messageRepository.GetBySessionAsync(sessionId, ct);
        var existingCount = existingMessages.Count;

        // 跳过已存在的消息，只保存新消息
        // Orchestrator 返回的 messages 包含完整历史 + 新的工具调用消息
        for (var i = existingCount; i < messages.Count; i++)
        {
            var chatMsg = messages[i];

            // 跳过最后一条助手响应（会在 SendMessageAsync 中单独保存）
            if (i == messages.Count - 1 && chatMsg.Role == "assistant" && chatMsg.ToolCalls == null)
            {
                continue;
            }

            // 将 ChatMessage 转换为 Message 并保存
            var role = chatMsg.Role switch
            {
                "user" => MessageRole.User,
                "assistant" => MessageRole.Assistant,
                "system" => MessageRole.System,
                "tool" => MessageRole.Assistant, // 工具结果作为 Assistant 消息保存
                _ => MessageRole.Assistant
            };

            var message = new Message
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                Role = role,
                Content = chatMsg.Content,
                CreatedAt = DateTime.UtcNow
            };

            await _messageRepository.CreateAsync(message, ct);

            _logger.LogDebug("保存对话消息: Role={Role}, Content={Content}",
                role, chatMsg.Content.Length > 50 ? chatMsg.Content[..50] + "..." : chatMsg.Content);
        }
    }
}
