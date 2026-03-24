using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace GeneralAgent.Infrastructure.Skills.Executors;

/// <summary>
/// 技能执行器实现
/// 渲染 Scriban 模板后调用 LLM，返回智能响应
/// </summary>
public class SkillExecutor : ISkillExecutor
{
    private readonly ILLMClientFactory _llmFactory;
    private readonly IMessageRepository _messageRepo;
    private readonly ILogger<SkillExecutor> _logger;

    public SkillExecutor(
        ILLMClientFactory llmFactory,
        IMessageRepository messageRepo,
        ILogger<SkillExecutor> logger)
    {
        _llmFactory = llmFactory;
        _messageRepo = messageRepo;
        _logger = logger;
    }

    public async Task<Result<string>> ExecuteAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("执行技能: {SkillName}, 会话: {SessionId}", skill.FullName, sessionId);

            // 1. 验证和准备参数
            var validationResult = ValidateArguments(skill, arguments);
            if (!validationResult.IsSuccess)
            {
                return Result<string>.Failure(validationResult.Error!);
            }

            var preparedArgs = new Dictionary<string, object>(validationResult.Value!);

            // 2. 注入上下文（如果需要）
            if (skill.RequiresContext)
            {
                var contextResult = await GetContextMessagesAsync(sessionId, skill.ContextConfig, ct);
                if (!contextResult.IsSuccess)
                {
                    return Result<string>.Failure(contextResult.Error!);
                }

                preparedArgs["context"] = contextResult.Value!;
            }

            // 3. 渲染 Scriban 模板
            var renderResult = RenderTemplate(skill.Template, preparedArgs);
            if (!renderResult.IsSuccess)
            {
                return Result<string>.Failure(renderResult.Error!);
            }

            var prompt = renderResult.Value!;
            _logger.LogDebug("模板渲染完成，提示词长度: {Length}", prompt.Length);

            // 4. 调用 LLM
            var client = _llmFactory.GetClient(providerName);
            var request = new CompletionRequest
            {
                Model = "default", // 使用客户端的默认模型
                Messages = new[]
                {
                    new ChatMessage
                    {
                        Role = "user",
                        Content = prompt
                    }
                }
            };

            var response = await client.CompleteAsync(request, ct);

            _logger.LogInformation(
                "技能执行成功: {SkillName}, Tokens: {Tokens}",
                skill.FullName,
                response.Usage.TotalTokens);

            return Result<string>.Success(response.Content ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行技能失败: {SkillName}", skill.FullName);
            return Result<string>.Failure($"执行技能失败: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<string> ExecuteStreamAsync(
        Skill skill,
        Dictionary<string, object> arguments,
        Guid sessionId,
        string? providerName = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogDebug("流式执行技能: {SkillName}, 会话: {SessionId}", skill.FullName, sessionId);

        // 1. 验证和准备参数
        var validationResult = ValidateArguments(skill, arguments);
        if (!validationResult.IsSuccess)
        {
            _logger.LogError("参数验证失败: {Error}", validationResult.Error);
            yield break;
        }

        var preparedArgs = new Dictionary<string, object>(validationResult.Value!);

        // 2. 注入上下文（如果需要）
        if (skill.RequiresContext)
        {
            var contextResult = await GetContextMessagesAsync(sessionId, skill.ContextConfig, ct);
            if (!contextResult.IsSuccess)
            {
                _logger.LogError("获取上下文失败: {Error}", contextResult.Error);
                yield break;
            }

            preparedArgs["context"] = contextResult.Value!;
        }

        // 3. 渲染 Scriban 模板
        var renderResult = RenderTemplate(skill.Template, preparedArgs);
        if (!renderResult.IsSuccess)
        {
            _logger.LogError("模板渲染失败: {Error}", renderResult.Error);
            yield break;
        }

        var prompt = renderResult.Value!;
        _logger.LogDebug("模板渲染完成，提示词长度: {Length}", prompt.Length);

        // 4. 流式调用 LLM
        var client = _llmFactory.GetClient(providerName);
        var request = new CompletionRequest
        {
            Model = "default",
            Messages = new[]
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = prompt
                }
            }
        };

        await foreach (var chunk in client.StreamAsync(request, ct))
        {
            yield return chunk.Delta;
        }

        _logger.LogInformation("技能流式执行完成: {SkillName}", skill.FullName);
    }

    /// <summary>
    /// 验证参数并应用默认值
    /// </summary>
    private Result<Dictionary<string, object>> ValidateArguments(
        Skill skill,
        Dictionary<string, object> arguments)
    {
        var preparedArgs = new Dictionary<string, object>(arguments);

        foreach (var parameter in skill.Parameters)
        {
            var hasValue = arguments.TryGetValue(parameter.Name, out var value);

            // 验证参数
            var validationResult = parameter.Validate(hasValue ? value : null);

            if (!validationResult.IsSuccess)
            {
                _logger.LogWarning("参数验证失败: {Parameter} - {Error}",
                    parameter.Name, validationResult.Error);
                return Result<Dictionary<string, object>>.Failure(validationResult.Error!);
            }

            // 使用验证后的值（可能包含默认值）
            if (validationResult.Value != null)
            {
                preparedArgs[parameter.Name] = validationResult.Value;
            }
        }

        return Result<Dictionary<string, object>>.Success(preparedArgs);
    }

    /// <summary>
    /// 获取上下文消息
    /// </summary>
    private async Task<Result<object>> GetContextMessagesAsync(
        Guid sessionId,
        ContextConfig? config,
        CancellationToken ct)
    {
        try
        {
            config ??= new ContextConfig();

            // 获取最近的消息
            var messages = await _messageRepo.GetRecentAsync(sessionId, config.MaxMessages, ct);

            // 过滤消息
            var filteredMessages = messages.AsEnumerable();

            // 过滤系统消息
            if (!config.IncludeSystemMessages)
            {
                filteredMessages = filteredMessages.Where(m => m.Role != MessageRole.System);
            }

            // 过滤角色
            if (config.Roles != null && config.Roles.Length > 0)
            {
                var allowedRoles = config.Roles
                    .Select(r => Enum.TryParse<MessageRole>(r, true, out var role) ? role : (MessageRole?)null)
                    .Where(r => r.HasValue)
                    .Select(r => r!.Value)
                    .ToHashSet();

                filteredMessages = filteredMessages.Where(m => allowedRoles.Contains(m.Role));
            }

            // 转换为上下文对象
            var context = new
            {
                messages = filteredMessages.Select(m => new
                {
                    role = m.Role.ToString().ToLower(),
                    content = m.Content
                }).ToList()
            };

            _logger.LogDebug("获取上下文消息: {Count} 条", context.messages.Count);

            return Result<object>.Success(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取上下文消息失败");
            return Result<object>.Failure($"获取上下文消息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 渲染 Scriban 模板
    /// </summary>
    private Result<string> RenderTemplate(string templateText, Dictionary<string, object> arguments)
    {
        try
        {
            // 解析模板
            var template = Template.Parse(templateText);
            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                _logger.LogError("模板解析失败: {Errors}", errors);
                return Result<string>.Failure($"模板解析失败: {errors}");
            }

            // 创建模板上下文
            var scriptObject = new ScriptObject();
            foreach (var (key, value) in arguments)
            {
                scriptObject.Add(key, value);
            }

            var context = new TemplateContext();

            // 启用内置函数
            context.BuiltinObject.Import(typeof(Scriban.Functions.StringFunctions));
            context.BuiltinObject.Import(typeof(Scriban.Functions.MathFunctions));

            context.PushGlobal(scriptObject);

            // 渲染模板
            var output = template.Render(context);

            return Result<string>.Success(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "模板渲染失败");
            return Result<string>.Failure($"模板渲染失败: {ex.Message}");
        }
    }
}
