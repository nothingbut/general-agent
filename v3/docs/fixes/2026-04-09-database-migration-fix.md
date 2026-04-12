# 数据库迁移修复 (2026-04-09)

## 问题描述

运行 `dotnet run` 时出现以下错误：

```
warn: Microsoft.EntityFrameworkCore.Model.Validation[10620]
      The property 'Message.Metadata' is a collection or enumeration type with a value converter but with no value comparer.
warn: Microsoft.EntityFrameworkCore.Model.Validation[10620]
      The property 'ExtractionRecord.Metadata' is a collection or enumeration type with a value converter but with no value comparer.
启动失败: An error was generated for warning 'Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning': 
The model for context 'AgentDbContext' has pending changes. Add a new migration before updating the database.
```

## 根本原因

1. **缺少数据库迁移**: `ExtractionRecord` 表已添加到 `AgentDbContext`，但没有创建相应的 EF Core migration。
2. **缺少 Value Comparer**: `Message.Metadata` 和 `ExtractionRecord.Metadata` 是 Dictionary 类型，使用了 value converter 但没有配置 value comparer，导致 EF Core 无法正确比较集合元素。

## 修复步骤

### 1. 创建数据库迁移

```bash
# 安装 EF Core 工具
dotnet tool install --global dotnet-ef

# 还原项目工具
dotnet tool restore

# 创建迁移
dotnet ef migrations add AddExtractionRecords \
  --project src/GeneralAgent.Infrastructure \
  --startup-project src/GeneralAgent.Hosts.Console
```

**生成的文件**:
- `20260409025221_AddExtractionRecords.cs` - 迁移脚本
- `20260409025221_AddExtractionRecords.Designer.cs` - 设计器文件
- `AgentDbContextModelSnapshot.cs` - 更新的模型快照

### 2. 配置 Value Comparer

修改 `AgentDbContext.cs`，在 `OnModelCreating` 方法中添加：

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // 应用所有配置
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);

    // 配置 Message.Metadata 的 value comparer
    modelBuilder.Entity<Message>()
        .Property(m => m.Metadata)
        .Metadata.SetValueComparer(
            new ValueComparer<Dictionary<string, JsonElement>?>(
                (c1, c2) => JsonSerializer.Serialize(c1) == JsonSerializer.Serialize(c2),
                c => c == null ? 0 : JsonSerializer.Serialize(c).GetHashCode(),
                c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(c))
            )
        );

    // 配置 ExtractionRecord.Metadata 的 value comparer
    modelBuilder.Entity<ExtractionRecord>()
        .Property(e => e.Metadata)
        .Metadata.SetValueComparer(
            new ValueComparer<Dictionary<string, object>?>(
                (c1, c2) => JsonSerializer.Serialize(c1) == JsonSerializer.Serialize(c2),
                c => c == null ? 0 : JsonSerializer.Serialize(c).GetHashCode(),
                c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(c))
            )
        );
}
```

### 3. 抑制 EF Core 详细日志

修改 `Program.cs`，添加日志过滤器：

```csharp
// 抑制 EF Core 详细日志
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Migrations", LogLevel.Warning);
```

## 验证修复

### 编译项目

```bash
cd v3
dotnet build --configuration Release
```

**结果**: ✅ 0 警告，0 错误

### 运行测试

```bash
# 单元测试
dotnet test --filter "FullyQualifiedName~ScheduledTasks"
# 结果: 99/99 通过

# E2E 测试
dotnet test tests/GeneralAgent.Integration.Tests --filter "FullyQualifiedName~ScheduledTasks"
# 结果: 12/12 通过
```

### 测试应用程序

```bash
cd src/GeneralAgent.Hosts.Console

# 创建任务
dotnet run -- task schedule "测试任务" \
  --schedule "*/5 * * * *" \
  --type reminder \
  --payload '{"message":"测试"}' \
  --description "测试任务"

# 列出任务
dotnet run -- task list

# 删除任务
dotnet run -- task delete <task-id> --force
```

**结果**: ✅ 所有命令正常工作，输出清晰

## 影响范围

### 修改的文件

1. **src/GeneralAgent.Infrastructure/Storage/AgentDbContext.cs**
   - 添加 `System.Text.Json` 和 `Microsoft.EntityFrameworkCore.ChangeTracking` using
   - 在 `OnModelCreating` 中配置 value comparer

2. **src/GeneralAgent.Hosts.Console/Program.cs**
   - 添加 EF Core 日志过滤器

3. **新增迁移文件**
   - `20260409025221_AddExtractionRecords.cs`
   - `20260409025221_AddExtractionRecords.Designer.cs`
   - `AgentDbContextModelSnapshot.cs` (更新)

### 数据库变更

新增 `ExtractionRecords` 表：

```sql
CREATE TABLE "ExtractionRecords" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Timestamp" TEXT NOT NULL,
    "SessionId" TEXT,
    "SkillName" TEXT NOT NULL,
    "SkillNamespace" TEXT NOT NULL,
    "Action" INTEGER NOT NULL,
    "Confidence" REAL NOT NULL,
    "Occurrences" INTEGER NOT NULL,
    "RejectionReason" TEXT,
    "Metadata" TEXT
);
```

## 技术说明

### Value Comparer 的作用

EF Core 使用 value comparer 来：
1. **检测变更**: 比较实体属性的新旧值，判断是否需要更新
2. **哈希计算**: 为查询和缓存生成哈希码
3. **快照创建**: 创建属性值的独立副本

对于复杂类型（如 Dictionary），默认的引用比较不够，需要深度比较。

### Value Comparer 实现

我们使用 JSON 序列化来实现深度比较：

```csharp
new ValueComparer<Dictionary<string, T>?>(
    // 比较: 序列化后比较 JSON 字符串
    (c1, c2) => JsonSerializer.Serialize(c1) == JsonSerializer.Serialize(c2),
    
    // 哈希: 使用序列化后的字符串计算哈希
    c => c == null ? 0 : JsonSerializer.Serialize(c).GetHashCode(),
    
    // 快照: 序列化后反序列化创建独立副本
    c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, T>>(JsonSerializer.Serialize(c))
)
```

**优点**:
- 简单可靠
- 支持嵌套结构
- 与存储格式一致（JSON）

**缺点**:
- 性能开销（序列化）
- 对于大型 Dictionary 可能较慢

**替代方案**: 如果性能成为瓶颈，可以考虑：
1. 自定义深度比较算法
2. 使用哈希表缓存
3. 只比较关键字段

## 后续建议

1. **性能监控**: 监控 Metadata 字段的更新性能
2. **索引优化**: 如果频繁查询 Metadata，考虑添加 JSON 列索引
3. **数据大小限制**: 限制 Metadata 字段的大小，避免存储过大数据

## 相关文档

- [EF Core Value Comparers](https://learn.microsoft.com/en-us/ef/core/modeling/value-comparers)
- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [计划任务用户指南](../features/scheduled-tasks-user-guide.md)

---

**修复时间**: 2026-04-09  
**修复者**: Claude Sonnet 4.5  
**版本**: V3.2.0
