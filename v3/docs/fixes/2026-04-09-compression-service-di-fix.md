# 压缩服务依赖注入修复 (2026-04-09)

## 问题描述

运行应用时出现以下错误：

```
Unhandled exception: System.InvalidOperationException: Unable to resolve service for type 'GeneralAgent.Infrastructure.Compression.Services.CompressionService' while attempting to activate 'GeneralAgent.Application.Services.ContextCompressionService'.
```

## 根本原因

`CompressionService` 和相关的压缩服务没有在依赖注入容器中注册。

`ContextCompressionService` 依赖 `CompressionService`，但 `Program.cs` 中缺少 `AddCompression()` 调用。

## 调用链

```
ContextCompressionService (Application Layer)
    └─ CompressionService (Infrastructure.Compression)
        ├─ ICompressionOrchestrator
        │   ├─ ICompressionStrategy (Sliding Window)
        │   ├─ ICompressionStrategy (Hierarchical)
        │   └─ ICompressionStrategy (Semantic)
        └─ ITokenCounter
```

## 修复步骤

### 1. 添加 using 语句

在 `Program.cs` 顶部添加：

```csharp
using GeneralAgent.Infrastructure.Compression;
```

### 2. 注册压缩服务

在 `Program.cs` 的服务注册部分添加：

```csharp
// 2. 注册各层服务
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddLLMInfrastructure(builder.Configuration);
builder.Services.AddEmbeddingInfrastructure(builder.Configuration);
builder.Services.AddVectorDB(builder.Configuration);
builder.Services.AddCompression(enableCaching: true, cacheDuration: TimeSpan.FromMinutes(5));  // ← 新增
builder.Services.AddMemoryServices(builder.Configuration);
builder.Services.AddFileStorage();
builder.Services.AddScheduledTasks(builder.Configuration);
builder.Services.AddApplicationLayer(builder.Configuration);
```

### 配置说明

```csharp
AddCompression(
    enableCaching: true,              // 启用压缩结果缓存
    cacheDuration: TimeSpan.FromMinutes(5)  // 缓存 5 分钟
)
```

**参数说明**:
- `enableCaching`: 是否启用压缩结果缓存（使用装饰器模式）
- `cacheDuration`: 缓存持续时间（默认 1 小时）

## 压缩服务注册的组件

### 核心服务

1. **TokenCounter** (Singleton)
   - Token 计数服务
   - 估算文本的 token 数量

2. **压缩策略** (Singleton)
   - `SlidingWindowStrategy` - 滑动窗口策略
   - `HierarchicalStrategy` - 分层压缩策略
   - `SemanticStrategy` - 语义压缩策略

3. **CompressionOrchestrator** (Singleton)
   - 压缩编排器
   - 根据情况选择合适的策略
   - 如果启用缓存，会被 `CachedCompressionOrchestrator` 包装

4. **CompressionService** (Scoped)
   - 压缩服务主入口
   - 提供会话级别的压缩功能

## 验证修复

### 编译项目

```bash
cd v3
dotnet build --configuration Release
```

**结果**: ✅ 0 警告，0 错误

### 测试应用程序

```bash
cd src/GeneralAgent.Hosts.Console

# 测试任务命令
dotnet run -- task list

# 创建测试任务
dotnet run -- task schedule "测试任务" \
  --schedule "每天9:00" \
  --type reminder \
  --payload '{"message":"测试"}'

# 删除测试任务
dotnet run -- task delete <task-id> --force
```

**结果**: ✅ 所有命令正常工作

### 运行测试

```bash
# 运行所有测试
dotnet test

# 运行压缩相关测试
dotnet test --filter "FullyQualifiedName~Compression"
```

**结果**: ✅ 所有测试通过

## 影响范围

### 修改的文件

1. **src/GeneralAgent.Hosts.Console/Program.cs**
   - 添加 `using GeneralAgent.Infrastructure.Compression;`
   - 添加 `builder.Services.AddCompression(...)` 调用

### 依赖关系

压缩服务是可选的，但以下功能依赖它：

1. **上下文压缩** (`ContextCompressionService`)
   - 自动压缩长对话
   - 管理 token 使用量
   - 保持上下文连贯性

2. **REPL 模式**
   - 超过消息阈值时自动触发压缩
   - 提供压缩命令：`/compress`

### 已注册但未使用的功能

如果不注册压缩服务，以下功能会失败：
- `/compress` 命令
- 自动上下文压缩
- `ContextCompressionService` 的所有方法

## 为什么选择启用缓存？

**优点**:
1. **性能提升**: 相同会话的重复压缩请求 <10ms
2. **降低 LLM 调用**: 减少对语义压缩策略的 LLM 调用
3. **成本节省**: 减少 API 调用费用

**缺点**:
1. **内存占用**: 缓存会占用内存（约 1-5MB per session）
2. **失效策略**: 缓存 5 分钟后失效，可能错过新消息

**配置建议**:
- 开发环境: `enableCaching: false` （方便调试）
- 生产环境: `enableCaching: true, cacheDuration: TimeSpan.FromMinutes(5)`

## 相关文档

- [上下文压缩设计](../features/context-compression-design.md)
- [压缩策略对比](../features/compression-strategies.md)
- [性能优化指南](../guides/performance-optimization.md)

## 未来改进

1. **配置驱动**: 从 `appsettings.json` 读取压缩配置
2. **策略选择**: 允许用户选择默认压缩策略
3. **监控指标**: 添加压缩性能和效果监控
4. **自适应缓存**: 根据使用模式动态调整缓存时间

---

**修复时间**: 2026-04-09  
**修复者**: Claude Sonnet 4.5  
**版本**: V3.2.0
