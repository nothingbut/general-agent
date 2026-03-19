using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Application.Services;

/// <summary>
/// Tool Calling 编排器
/// 负责管理多轮 Tool Calling 循环，包括用户确认、历史记录维护和限制保护
/// </summary>
public sealed class ToolCallingOrchestrator
{
    private readonly ToolExecutor _toolExecutor;
    private readonly ToolRegistry _registry;
    private readonly ILLMClient _llmClient;
    private readonly IToolCallingListener _listener;
    private readonly IToolSerializer _serializer;
    private readonly ToolCallingConfig _config;
    private readonly ILogger<ToolCallingOrchestrator> _logger;

    public ToolCallingOrchestrator(
        ToolExecutor toolExecutor,
        ToolRegistry registry,
        ILLMClient llmClient,
        IToolCallingListener listener,
        IToolSerializer serializer,
        IOptions<ToolCallingConfig> config,
        ILogger<ToolCallingOrchestrator> logger)
    {
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 执行对话循环（包括 Tool Calling）
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="history">对话历史</param>
    /// <param name="providerName">LLM 提供商名称（可选，用于选择特定客户端）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>对话结果</returns>
    public async Task<ConversationResult> ExecuteAsync(
        Guid sessionId,
        List<ChatMessage> history,
        string? providerName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(history);

        _logger.LogInformation("开始对话循环: SessionId={SessionId}, Enabled={Enabled}",
            sessionId, _config.Enabled);

        var messages = new List<ChatMessage>(history);
        var currentRounds = 0;
        var totalToolCalls = 0;
        var maxRounds = _config.MaxRounds;

        try
        {
            // 如果 Tool Calling 未启用，直接调用 LLM
            if (!_config.Enabled)
            {
                _logger.LogDebug("Tool Calling 未启用，直接调用 LLM");
                var response = await CallLLMAsync(messages, null, ct);
                return new ConversationResult
                {
                    FinalResponse = response.Content,
                    TotalRounds = 0,
                    TotalToolCalls = 0,
                    Messages = messages,
                    Truncated = false
                };
            }

            // 获取所有注册的工具并序列化
            var tools = _registry.GetAllTools();
            var toolDefinitions = tools.Select(t => t.GetDefinition()).ToList();

            _logger.LogDebug("工具总数: {ToolCount}", toolDefinitions.Count);

            var serializedTools = toolDefinitions.Count > 0
                ? _serializer.SerializeTools(toolDefinitions)
                : null;

            // Tool Calling 循环
            while (currentRounds < _config.AbsoluteMaxRounds)
            {
                ct.ThrowIfCancellationRequested();

                // 调用 LLM
                var response = await CallLLMAsync(messages, serializedTools, ct);

                // 如果没有工具调用，返回最终响应
                if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                {
                    _logger.LogInformation(
                        "对话完成: Rounds={Rounds}, ToolCalls={ToolCalls}",
                        currentRounds,
                        totalToolCalls);

                    return new ConversationResult
                    {
                        FinalResponse = response.Content,
                        TotalRounds = currentRounds,
                        TotalToolCalls = totalToolCalls,
                        Messages = messages,
                        Truncated = false
                    };
                }

                // 增加轮数计数
                currentRounds++;
                totalToolCalls += response.ToolCalls.Count;

                _logger.LogDebug(
                    "LLM 调用了 {Count} 个工具 (Round {CurrentRounds}/{MaxRounds})",
                    response.ToolCalls.Count,
                    currentRounds,
                    maxRounds);

                // 检查是否达到最大轮数
                if (currentRounds >= maxRounds)
                {
                    _logger.LogInformation("达到最大轮数 {MaxRounds}，询问用户", maxRounds);

                    // 询问用户是否继续
                    var decision = await _listener.OnMaxRoundsReachedAsync(
                        currentRounds,
                        sessionId,
                        response.ToolCalls,
                        ct);

                    if (decision.Stop)
                    {
                        _logger.LogInformation("用户选择停止");
                        return new ConversationResult
                        {
                            FinalResponse = response.Content,
                            TotalRounds = currentRounds,
                            TotalToolCalls = totalToolCalls,
                            Messages = messages,
                            Truncated = true,
                            TruncationReason = $"用户选择停止（已执行 {currentRounds} 轮）"
                        };
                    }

                    // 用户选择延长
                    maxRounds += decision.ExtendBy;
                    _logger.LogInformation(
                        "用户选择延长 {ExtendBy} 轮，新限制: {MaxRounds}",
                        decision.ExtendBy,
                        maxRounds);
                }

                // 添加助手消息（包含工具调用）
                messages.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = response.Content,
                    ToolCalls = response.ToolCalls
                });

                // 执行工具调用
                var context = new ToolExecutionContext
                {
                    SessionId = sessionId,
                    ProviderName = providerName
                };

                var results = await _toolExecutor.ExecuteManyAsync(
                    response.ToolCalls,
                    context,
                    timeout: null,
                    ct);

                // 将工具结果添加到消息历史
                foreach (var result in results)
                {
                    var resultContent = result.IsSuccess
                        ? result.Content
                        : $"错误: {result.ErrorMessage}";

                    messages.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = resultContent,
                        ToolCallId = result.Call.Id
                    });

                    _logger.LogDebug(
                        "工具 {ToolName} 执行{Status}: {Content}",
                        result.Call.ToolName,
                        result.IsSuccess ? "成功" : "失败",
                        resultContent.Length > 100 ? resultContent.Substring(0, 100) + "..." : resultContent);
                }
            }

            // 达到绝对最大轮数
            _logger.LogWarning("达到绝对最大轮数 {AbsoluteMaxRounds}", _config.AbsoluteMaxRounds);

            return new ConversationResult
            {
                FinalResponse = "抱歉，对话已达到最大轮数限制。",
                TotalRounds = currentRounds,
                TotalToolCalls = totalToolCalls,
                Messages = messages,
                Truncated = true,
                TruncationReason = $"达到绝对最大轮数限制（{_config.AbsoluteMaxRounds} 轮）"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("对话循环被取消");
            return new ConversationResult
            {
                FinalResponse = "对话已取消。",
                TotalRounds = currentRounds,
                TotalToolCalls = totalToolCalls,
                Messages = messages,
                Truncated = true,
                TruncationReason = "用户取消操作"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "对话循环执行失败");
            return new ConversationResult
            {
                FinalResponse = $"对话执行失败: {ex.Message}",
                TotalRounds = currentRounds,
                TotalToolCalls = totalToolCalls,
                Messages = messages,
                Truncated = true,
                TruncationReason = $"异常: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 调用 LLM
    /// </summary>
    private async Task<CompletionResponse> CallLLMAsync(
        List<ChatMessage> messages,
        System.Text.Json.Nodes.JsonArray? tools,
        CancellationToken ct)
    {
        var request = new CompletionRequest
        {
            Model = "default", // 可以从配置中获取
            Messages = messages,
            Tools = tools,
            Temperature = 0.7
        };

        return await _llmClient.CompleteAsync(request, ct);
    }
}
