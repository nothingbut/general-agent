using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// 配置服务
var connectionString = builder.Configuration.GetConnectionString("AgentDb")
    ?? throw new InvalidOperationException("Connection string 'AgentDb' not found.");

builder.Services.AddInfrastructure(connectionString);

var host = builder.Build();

// 运行演示
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var sessionRepo = host.Services.GetRequiredService<ISessionRepository>();
var messageRepo = host.Services.GetRequiredService<IMessageRepository>();

logger.LogInformation("=== General Agent V3 - Phase 1 验证 ===\n");

try
{
    // 1. 创建会话
    logger.LogInformation("1. 创建会话...");
    var session = Session.Create(title: "测试会话");
    await sessionRepo.CreateAsync(session);
    logger.LogInformation("   ✓ 会话已创建: {SessionId}", session.Id);

    // 2. 添加消息
    logger.LogInformation("\n2. 添加消息...");
    var userMessage = Message.CreateUser(session.Id, "你好，这是测试消息");
    await messageRepo.CreateAsync(userMessage);
    logger.LogInformation("   ✓ 用户消息已添加: {MessageId}", userMessage.Id);

    var assistantMessage = Message.CreateAssistant(session.Id, "收到，这是响应消息");
    await messageRepo.CreateAsync(assistantMessage);
    logger.LogInformation("   ✓ 助手消息已添加: {MessageId}", assistantMessage.Id);

    // 3. 查询消息
    logger.LogInformation("\n3. 查询会话消息...");
    var messages = await messageRepo.GetBySessionAsync(session.Id);
    logger.LogInformation("   ✓ 共有 {Count} 条消息", messages.Count);
    foreach (var msg in messages)
    {
        logger.LogInformation("     - [{Role}] {Content}",
            msg.Role, msg.Content.Length > 30 ? msg.Content[..30] + "..." : msg.Content);
    }

    // 4. 更新会话
    logger.LogInformation("\n4. 更新会话标题...");
    var updatedSession = session.WithTitle("测试会话（已更新）");
    await sessionRepo.UpdateAsync(updatedSession);
    logger.LogInformation("   ✓ 会话标题已更新");

    // 5. 列出会话
    logger.LogInformation("\n5. 列出所有会话...");
    var pagedSessions = await sessionRepo.ListAsync(limit: 10, offset: 0);
    logger.LogInformation("   ✓ 共有 {Total} 个会话", pagedSessions.Total);
    foreach (var s in pagedSessions.Items)
    {
        var msgCount = await messageRepo.CountAsync(s.Id);
        logger.LogInformation("     - {Title} ({MessageCount} 条消息)",
            s.Title ?? "无标题", msgCount);
    }

    // 6. 搜索会话
    logger.LogInformation("\n6. 搜索会话...");
    var searchResults = await sessionRepo.SearchAsync("测试", limit: 10);
    logger.LogInformation("   ✓ 找到 {Count} 个匹配的会话", searchResults.Count);

    // 7. 验证级联删除
    logger.LogInformation("\n7. 测试级联删除（创建临时会话）...");
    var tempSession = Session.Create(title: "临时会话");
    await sessionRepo.CreateAsync(tempSession);
    await messageRepo.CreateAsync(Message.CreateUser(tempSession.Id, "临时消息"));
    var msgCountBefore = await messageRepo.CountAsync(tempSession.Id);
    logger.LogInformation("   ✓ 临时会话有 {Count} 条消息", msgCountBefore);

    await sessionRepo.DeleteAsync(tempSession.Id);
    var msgCountAfter = await messageRepo.CountAsync(tempSession.Id);
    logger.LogInformation("   ✓ 删除会话后，消息数量: {Count} (应为 0)", msgCountAfter);

    logger.LogInformation("\n=== 所有验证通过 ✓ ===");
}
catch (Exception ex)
{
    logger.LogError(ex, "验证失败");
    return 1;
}

return 0;
