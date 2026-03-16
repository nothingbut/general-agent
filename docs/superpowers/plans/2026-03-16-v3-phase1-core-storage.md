# V3 Phase 1: Core + Storage 实施计划

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 V3 的核心抽象层和数据持久化层，提供会话和消息的 CRUD 操作。Application 层留到 Phase 2。

**Architecture:** 分层架构 - Core 层定义纯净的领域模型和接口，Infrastructure.Storage 层使用 EF Core + SQLite 实现持久化。所有类型使用 record 实现不可变性。验证用 Console 应用直接调用 Repository。

**Tech Stack:** .NET 9, C# 12, Entity Framework Core 9, SQLite, xUnit, FluentAssertions

**参考文档:**
- 设计规范: `docs/superpowers/specs/2026-03-16-v3-csharp-architecture-design.md`
- V2 架构: `v2/docs/ARCHITECTURE.md`

---

## 文件结构

### 创建的文件

**核心层 (GeneralAgent.Core):**
- `v3/src/GeneralAgent.Core/GeneralAgent.Core.csproj` - 项目文件
- `v3/src/GeneralAgent.Core/Models/Session.cs` - 会话模型
- `v3/src/GeneralAgent.Core/Models/Message.cs` - 消息模型
- `v3/src/GeneralAgent.Core/Models/MessageRole.cs` - 消息角色枚举
- `v3/src/GeneralAgent.Core/Models/SessionType.cs` - 会话类型枚举
- `v3/src/GeneralAgent.Core/Models/SessionStatus.cs` - 会话状态枚举
- `v3/src/GeneralAgent.Core/Abstractions/ISessionRepository.cs` - 会话仓储接口
- `v3/src/GeneralAgent.Core/Abstractions/IMessageRepository.cs` - 消息仓储接口
- `v3/src/GeneralAgent.Core/Common/PagedResult.cs` - 分页结果
- `v3/src/GeneralAgent.Core/Common/Result.cs` - Result 模式
- `v3/src/GeneralAgent.Core/Exceptions/AgentException.cs` - 基础异常
- `v3/src/GeneralAgent.Core/Exceptions/StorageException.cs` - 存储异常

**存储层 (GeneralAgent.Infrastructure):**
- `v3/src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj` - 项目文件
- `v3/src/GeneralAgent.Infrastructure/Storage/AgentDbContext.cs` - EF Core 上下文
- `v3/src/GeneralAgent.Infrastructure/Storage/Configurations/SessionConfiguration.cs` - Session 配置
- `v3/src/GeneralAgent.Infrastructure/Storage/Configurations/MessageConfiguration.cs` - Message 配置
- `v3/src/GeneralAgent.Infrastructure/Storage/Repositories/SessionRepository.cs` - Session 仓储实现
- `v3/src/GeneralAgent.Infrastructure/Storage/Repositories/MessageRepository.cs` - Message 仓储实现
- `v3/src/GeneralAgent.Infrastructure/DependencyInjection.cs` - 依赖注入扩展

**应用层 (GeneralAgent.Application):**
- `v3/src/GeneralAgent.Application/GeneralAgent.Application.csproj` - 项目文件
- `v3/src/GeneralAgent.Application/Services/SessionService.cs` - 会话服务
- `v3/src/GeneralAgent.Application/DependencyInjection.cs` - 依赖注入扩展

**测试项目:**
- `v3/tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj` - Core 测试项目
- `v3/tests/GeneralAgent.Core.Tests/Models/SessionTests.cs` - Session 单元测试
- `v3/tests/GeneralAgent.Core.Tests/Models/MessageTests.cs` - Message 单元测试
- `v3/tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj` - Infrastructure 测试
- `v3/tests/GeneralAgent.Infrastructure.Tests/Storage/SessionRepositoryTests.cs` - Repository 测试
- `v3/tests/GeneralAgent.Infrastructure.Tests/Storage/MessageRepositoryTests.cs` - Repository 测试
- `v3/tests/GeneralAgent.Application.Tests/GeneralAgent.Application.Tests.csproj` - Application 测试
- `v3/tests/GeneralAgent.Application.Tests/Services/SessionServiceTests.cs` - Service 测试

**宿主程序 (简单测试用):**
- `v3/src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj` - Console 项目
- `v3/src/GeneralAgent.Hosts.Console/Program.cs` - 入口程序
- `v3/src/GeneralAgent.Hosts.Console/appsettings.json` - 配置文件

**解决方案:**
- `v3/GeneralAgent.sln` - 解决方案文件
- `v3/Directory.Build.props` - 全局属性
- `v3/Directory.Packages.props` - 中央包管理

---

## Chunk 1: 项目初始化和核心模型

### Task 1: 创建解决方案和项目结构

**Files:**
- Create: `v3/GeneralAgent.sln`
- Create: `v3/Directory.Build.props`
- Create: `v3/Directory.Packages.props`
- Create: `v3/.gitignore`

- [ ] **Step 1: 创建 v3 目录和解决方案**

```bash
cd /Users/shichang/Workspace/projects/ai-powered/general-agent
mkdir -p v3
cd v3
dotnet new sln -n GeneralAgent
```

- [ ] **Step 2: 创建全局属性文件**

创建 `v3/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: 创建中央包管理文件**

创建 `v3/Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- EF Core -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />

    <!-- Testing -->
    <PackageVersion Include="xunit" Version="2.9.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="FluentAssertions" Version="6.12.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />

    <!-- Hosting -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: 创建 .gitignore**

创建 `v3/.gitignore`:

```
bin/
obj/
*.user
*.suo
.vs/
```

- [ ] **Step 5: 验证配置**

```bash
cd v3
dotnet --version  # 确认 .NET 9 已安装
```

预期: 输出 `9.0.x`

- [ ] **Step 6: 提交初始结构**

```bash
git add v3/
git commit -m "feat(v3): 初始化项目结构和全局配置"
```

---

### Task 2: 创建 Core 项目和领域模型

**Files:**
- Create: `v3/src/GeneralAgent.Core/GeneralAgent.Core.csproj`
- Create: `v3/src/GeneralAgent.Core/Models/SessionType.cs`
- Create: `v3/src/GeneralAgent.Core/Models/SessionStatus.cs`
- Create: `v3/src/GeneralAgent.Core/Models/MessageRole.cs`

- [ ] **Step 1: 创建 Core 项目**

```bash
cd v3
mkdir -p src/GeneralAgent.Core/Models
dotnet new classlib -n GeneralAgent.Core -o src/GeneralAgent.Core
dotnet sln add src/GeneralAgent.Core/GeneralAgent.Core.csproj
```

- [ ] **Step 2: 删除默认生成的 Class1.cs**

```bash
rm src/GeneralAgent.Core/Class1.cs
```

- [ ] **Step 3: 创建 SessionType 枚举**

创建 `v3/src/GeneralAgent.Core/Models/SessionType.cs`:

```csharp
namespace GeneralAgent.Core.Models;

/// <summary>
/// 会话类型
/// </summary>
public enum SessionType
{
    /// <summary>
    /// 普通会话
    /// </summary>
    Normal,

    /// <summary>
    /// 子代理会话
    /// </summary>
    Subagent
}
```

- [ ] **Step 4: 创建 SessionStatus 枚举**

创建 `v3/src/GeneralAgent.Core/Models/SessionStatus.cs`:

```csharp
namespace GeneralAgent.Core.Models;

/// <summary>
/// 会话状态
///
/// 状态转换规则：
/// - Normal 会话: Active (默认，保持不变)
/// - Subagent 会话: Active → Running → Completed/Failed
/// - 父会话: Active → Running (有子会话时) → Active (子会话完成)
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// 活跃中
    /// </summary>
    Active,

    /// <summary>
    /// 运行中（有子代理在执行）
    /// </summary>
    Running,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失败
    /// </summary>
    Failed
}
```

- [ ] **Step 5: 创建 MessageRole 枚举**

创建 `v3/src/GeneralAgent.Core/Models/MessageRole.cs`:

```csharp
namespace GeneralAgent.Core.Models;

/// <summary>
/// 消息角色
/// </summary>
public enum MessageRole
{
    /// <summary>
    /// 用户消息
    /// </summary>
    User,

    /// <summary>
    /// 助手消息
    /// </summary>
    Assistant,

    /// <summary>
    /// 系统消息
    /// </summary>
    System
}
```

- [ ] **Step 6: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Core/GeneralAgent.Core.csproj
```

预期: 编译成功，无警告

- [ ] **Step 7: 提交枚举类型**

```bash
git add v3/src/GeneralAgent.Core/
git commit -m "feat(v3-core): 添加领域枚举类型"
```

---

### Task 3: 实现 Session 模型（TDD）

**Files:**
- Create: `v3/tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj`
- Create: `v3/tests/GeneralAgent.Core.Tests/Models/SessionTests.cs`
- Create: `v3/src/GeneralAgent.Core/Models/Session.cs`

- [ ] **Step 1: 创建测试项目**

```bash
cd v3
mkdir -p tests/GeneralAgent.Core.Tests/Models
dotnet new xunit -n GeneralAgent.Core.Tests -o tests/GeneralAgent.Core.Tests
dotnet sln add tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj
dotnet add tests/GeneralAgent.Core.Tests reference src/GeneralAgent.Core
```

- [ ] **Step 2: 添加测试依赖**

编辑 `v3/tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj`，添加 FluentAssertions:

```xml
<ItemGroup>
  <PackageReference Include="FluentAssertions" />
  <PackageReference Include="xunit" />
  <PackageReference Include="xunit.runner.visualstudio" />
  <PackageReference Include="coverlet.collector" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
</ItemGroup>
```

- [ ] **Step 3: 删除默认测试文件**

```bash
rm tests/GeneralAgent.Core.Tests/UnitTest1.cs
```

- [ ] **Step 4: 编写 Session 测试（失败的）**

创建 `v3/tests/GeneralAgent.Core.Tests/Models/SessionTests.cs`:

```csharp
using FluentAssertions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Models;

public class SessionTests
{
    [Fact]
    public void Create_ShouldGenerateUniqueId()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_ShouldSetTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var session = Session.Create();
        var after = DateTime.UtcNow;

        // Assert
        session.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        session.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_WithTitle_ShouldSetTitle()
    {
        // Act
        var session = Session.Create(title: "Test Session");

        // Assert
        session.Title.Should().Be("Test Session");
    }

    [Fact]
    public void Create_WithoutTitle_ShouldHaveNullTitle()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Title.Should().BeNull();
    }

    [Fact]
    public void Create_WithParentId_ShouldBeSubagentType()
    {
        // Arrange
        var parentId = Guid.NewGuid();

        // Act
        var session = Session.Create(parentId: parentId);

        // Assert
        session.Type.Should().Be(SessionType.Subagent);
        session.ParentId.Should().Be(parentId);
    }

    [Fact]
    public void Create_WithoutParentId_ShouldBeNormalType()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Type.Should().Be(SessionType.Normal);
        session.ParentId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldDefaultToActiveStatus()
    {
        // Act
        var session = Session.Create();

        // Assert
        session.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public void WithTitle_ShouldReturnNewInstanceWithUpdatedTitle()
    {
        // Arrange
        var original = Session.Create(title: "Original");

        // Act
        var updated = original.WithTitle("Updated");

        // Assert
        updated.Should().NotBeSameAs(original);
        updated.Title.Should().Be("Updated");
        updated.Id.Should().Be(original.Id);
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    [Fact]
    public void WithStatus_ShouldReturnNewInstanceWithUpdatedStatus()
    {
        // Arrange
        var original = Session.Create();

        // Act
        var updated = original.WithStatus(SessionStatus.Completed);

        // Assert
        updated.Should().NotBeSameAs(original);
        updated.Status.Should().Be(SessionStatus.Completed);
        updated.Id.Should().Be(original.Id);
        updated.UpdatedAt.Should().BeAfter(original.UpdatedAt);
    }

    [Fact]
    public void Session_ShouldBeImmutable()
    {
        // Arrange
        var session = Session.Create(title: "Test");

        // Act & Assert
        // 以下代码不应编译（验证不可变性）
        // session.Title = "Modified";  // 应该报错
        // session.Status = SessionStatus.Completed;  // 应该报错
    }
}
```

- [ ] **Step 5: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj
```

预期: 所有测试失败，报错 "Session type not found"

- [ ] **Step 6: 实现 Session 模型**

创建 `v3/src/GeneralAgent.Core/Models/Session.cs`:

```csharp
namespace GeneralAgent.Core.Models;

/// <summary>
/// 会话实体（不可变）
/// </summary>
public sealed record Session
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 会话标题
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（UTC）
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 会话类型
    /// </summary>
    public SessionType Type { get; init; } = SessionType.Normal;

    /// <summary>
    /// 父会话 ID（Subagent 场景）
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// 会话状态
    /// </summary>
    public SessionStatus Status { get; init; } = SessionStatus.Active;

    /// <summary>
    /// 创建新会话
    /// </summary>
    public static Session Create(string? title = null, Guid? parentId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ParentId = parentId,
            Type = parentId.HasValue ? SessionType.Subagent : SessionType.Normal
        };

    /// <summary>
    /// 更新标题（返回新实例）
    /// </summary>
    public Session WithTitle(string? title)
        => this with { Title = title, UpdatedAt = DateTime.UtcNow };

    /// <summary>
    /// 更新状态（返回新实例）
    /// </summary>
    public Session WithStatus(SessionStatus status)
        => this with { Status = status, UpdatedAt = DateTime.UtcNow };
}
```

- [ ] **Step 7: 运行测试验证通过**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj
```

预期: 所有测试通过（10/10）

- [ ] **Step 8: 提交 Session 实现**

```bash
git add v3/
git commit -m "feat(v3-core): 实现 Session 不可变模型（TDD）"
```

---

### Task 4: 实现 Message 模型（TDD）

**Files:**
- Create: `v3/tests/GeneralAgent.Core.Tests/Models/MessageTests.cs`
- Create: `v3/src/GeneralAgent.Core/Models/Message.cs`

- [ ] **Step 1: 编写 Message 测试（失败的）**

创建 `v3/tests/GeneralAgent.Core.Tests/Models/MessageTests.cs`:

```csharp
using FluentAssertions;
using GeneralAgent.Core.Models;
using System.Text.Json;

namespace GeneralAgent.Core.Tests.Models;

public class MessageTests
{
    [Fact]
    public void CreateUser_ShouldGenerateUniqueId()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateUser(sessionId, "Hello");

        // Assert
        message.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateUser_ShouldSetUserRole()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateUser(sessionId, "Hello");

        // Assert
        message.Role.Should().Be(MessageRole.User);
    }

    [Fact]
    public void CreateUser_ShouldSetContent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateUser(sessionId, "Test content");

        // Assert
        message.Content.Should().Be("Test content");
        message.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public void CreateUser_ShouldSetTimestamp()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var message = Message.CreateUser(sessionId, "Hello");
        var after = DateTime.UtcNow;

        // Assert
        message.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void CreateAssistant_ShouldSetAssistantRole()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateAssistant(sessionId, "Response");

        // Assert
        message.Role.Should().Be(MessageRole.Assistant);
    }

    [Fact]
    public void CreateAssistant_WithMetadata_ShouldSetMetadata()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, JsonElement>
        {
            ["model"] = JsonSerializer.SerializeToElement("claude-3"),
            ["tokens"] = JsonSerializer.SerializeToElement(100)
        };

        // Act
        var message = Message.CreateAssistant(sessionId, "Response", metadata);

        // Assert
        message.Metadata.Should().NotBeNull();
        message.Metadata.Should().ContainKey("model");
        message.Metadata!["model"].GetString().Should().Be("claude-3");
    }

    [Fact]
    public void CreateAssistant_WithoutMetadata_ShouldHaveNullMetadata()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var message = Message.CreateAssistant(sessionId, "Response");

        // Assert
        message.Metadata.Should().BeNull();
    }

    [Fact]
    public void Message_ShouldBeImmutable()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var message = Message.CreateUser(sessionId, "Test");

        // Act & Assert
        // 以下代码不应编译（验证不可变性）
        // message.Content = "Modified";  // 应该报错
        // message.Role = MessageRole.Assistant;  // 应该报错
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj --filter "FullyQualifiedName~MessageTests"
```

预期: 所有测试失败，报错 "Message type not found"

- [ ] **Step 3: 实现 Message 模型**

创建 `v3/src/GeneralAgent.Core/Models/Message.cs`:

```csharp
using System.Text.Json;

namespace GeneralAgent.Core.Models;

/// <summary>
/// 消息实体（不可变）
/// </summary>
public sealed record Message
{
    /// <summary>
    /// 消息 ID
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// 消息角色
    /// </summary>
    public MessageRole Role { get; init; }

    /// <summary>
    /// 消息内容
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 创建时间（UTC）
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 元数据（可选，使用 JsonElement 保证类型安全）
    /// </summary>
    public Dictionary<string, JsonElement>? Metadata { get; init; }

    /// <summary>
    /// 创建用户消息
    /// </summary>
    public static Message CreateUser(Guid sessionId, string content)
        => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.User,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

    /// <summary>
    /// 创建助手消息
    /// </summary>
    public static Message CreateAssistant(
        Guid sessionId,
        string content,
        Dictionary<string, JsonElement>? metadata = null)
        => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Role = MessageRole.Assistant,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata
        };
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj --filter "FullyQualifiedName~MessageTests"
```

预期: 所有测试通过（8/8）

- [ ] **Step 5: 运行全部测试**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj
```

预期: 所有测试通过（18/18 - Session 10 + Message 8）

- [ ] **Step 6: 提交 Message 实现**

```bash
git add v3/
git commit -m "feat(v3-core): 实现 Message 不可变模型（TDD）"
```

---

## Chunk 2: 核心抽象和通用类型

### Task 5: 实现 Result 模式和 PagedResult

**Files:**
- Create: `v3/src/GeneralAgent.Core/Common/Result.cs`
- Create: `v3/src/GeneralAgent.Core/Common/PagedResult.cs`
- Create: `v3/tests/GeneralAgent.Core.Tests/Common/ResultTests.cs`
- Create: `v3/tests/GeneralAgent.Core.Tests/Common/PagedResultTests.cs`

- [ ] **Step 1: 编写 Result 测试**

创建 `v3/tests/GeneralAgent.Core.Tests/Common/ResultTests.cs`:

```csharp
using FluentAssertions;
using GeneralAgent.Core.Common;

namespace GeneralAgent.Core.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result<int>.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Act
        var result = Result<int>.Failure("Error message");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be("Error message");
    }

    [Fact]
    public void Match_WhenSuccess_ShouldCallSuccessFunc()
    {
        // Arrange
        var result = Result<int>.Success(42);

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error}");

        // Assert
        output.Should().Be("Success: 42");
    }

    [Fact]
    public void Match_WhenFailure_ShouldCallFailureFunc()
    {
        // Arrange
        var result = Result<int>.Failure("Something went wrong");

        // Act
        var output = result.Match(
            onSuccess: value => $"Success: {value}",
            onFailure: error => $"Failure: {error}");

        // Assert
        output.Should().Be("Failure: Something went wrong");
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj --filter "FullyQualifiedName~ResultTests"
```

预期: 测试失败

- [ ] **Step 3: 实现 Result 模式**

创建 `v3/src/GeneralAgent.Core/Common/Result.cs`:

```csharp
namespace GeneralAgent.Core.Common;

/// <summary>
/// 函数式错误处理结果类型
/// </summary>
/// <typeparam name="T">成功时的值类型</typeparam>
public readonly record struct Result<T>
{
    /// <summary>
    /// 成功时的值
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// 失败时的错误消息
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool IsSuccess => Error is null;

    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(string error)
    {
        Value = default;
        Error = error;
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
    /// 模式匹配
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj --filter "FullyQualifiedName~ResultTests"
```

预期: 所有测试通过（4/4）

- [ ] **Step 5: 编写 PagedResult 测试**

创建 `v3/tests/GeneralAgent.Core.Tests/Common/PagedResultTests.cs`:

```csharp
using FluentAssertions;
using GeneralAgent.Core.Common;

namespace GeneralAgent.Core.Tests.Common;

public class PagedResultTests
{
    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };

        // Act
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 0);

        // Assert
        result.Items.Should().BeEquivalentTo(items);
        result.Total.Should().Be(10);
        result.Limit.Should().Be(3);
        result.Offset.Should().Be(0);
    }

    [Fact]
    public void HasNextPage_WhenMoreItems_ShouldReturnTrue()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 0);

        // Assert
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenNoMoreItems_ShouldReturnFalse()
    {
        // Arrange
        var items = new List<int> { 8, 9, 10 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 9);

        // Assert
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOffsetIsZero_ShouldReturnFalse()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 0);

        // Assert
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOffsetIsNonZero_ShouldReturnTrue()
    {
        // Arrange
        var items = new List<int> { 4, 5, 6 };
        var result = new PagedResult<int>(items, total: 10, limit: 3, offset: 3);

        // Assert
        result.HasPreviousPage.Should().BeTrue();
    }
}
```

- [ ] **Step 6: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj --filter "FullyQualifiedName~PagedResultTests"
```

预期: 测试失败

- [ ] **Step 7: 实现 PagedResult**

创建 `v3/src/GeneralAgent.Core/Common/PagedResult.cs`:

```csharp
namespace GeneralAgent.Core.Common;

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">项目类型</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// 当前页的项目
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// 总项目数
    /// </summary>
    public int Total { get; }

    /// <summary>
    /// 每页限制
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// 偏移量
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// 是否有下一页
    /// </summary>
    public bool HasNextPage => Offset + Items.Count < Total;

    /// <summary>
    /// 是否有上一页
    /// </summary>
    public bool HasPreviousPage => Offset > 0;

    public PagedResult(IReadOnlyList<T> items, int total, int limit, int offset)
    {
        Items = items;
        Total = total;
        Limit = limit;
        Offset = offset;
    }
}
```

- [ ] **Step 8: 运行测试验证通过**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj --filter "FullyQualifiedName~PagedResultTests"
```

预期: 所有测试通过（5/5）

- [ ] **Step 9: 运行全部 Core 测试**

```bash
cd v3
dotnet test tests/GeneralAgent.Core.Tests/GeneralAgent.Core.Tests.csproj
```

预期: 所有测试通过（27/27）

- [ ] **Step 10: 提交通用类型实现**

```bash
git add v3/
git commit -m "feat(v3-core): 实现 Result 和 PagedResult 通用类型（TDD）"
```

---

### Task 6: 定义核心异常类型

**Files:**
- Create: `v3/src/GeneralAgent.Core/Exceptions/AgentException.cs`
- Create: `v3/src/GeneralAgent.Core/Exceptions/StorageException.cs`

- [ ] **Step 1: 实现 AgentException 基类**

创建 `v3/src/GeneralAgent.Core/Exceptions/AgentException.cs`:

```csharp
namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// Agent 系统基础异常
/// </summary>
public class AgentException : Exception
{
    public AgentException(string message) : base(message)
    {
    }

    public AgentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 2: 实现 StorageException**

创建 `v3/src/GeneralAgent.Core/Exceptions/StorageException.cs`:

```csharp
namespace GeneralAgent.Core.Exceptions;

/// <summary>
/// 存储层异常
/// </summary>
public sealed class StorageException : AgentException
{
    public StorageException(string message) : base(message)
    {
    }

    public StorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 3: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Core/GeneralAgent.Core.csproj
```

预期: 编译成功

- [ ] **Step 4: 提交异常类型**

```bash
git add v3/src/GeneralAgent.Core/Exceptions/
git commit -m "feat(v3-core): 添加异常类型定义"
```

---

### Task 7: 定义 Repository 接口

**Files:**
- Create: `v3/src/GeneralAgent.Core/Abstractions/ISessionRepository.cs`
- Create: `v3/src/GeneralAgent.Core/Abstractions/IMessageRepository.cs`

- [ ] **Step 1: 实现 ISessionRepository 接口**

创建 `v3/src/GeneralAgent.Core/Abstractions/ISessionRepository.cs`:

```csharp
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 会话仓储接口
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// 创建会话
    /// </summary>
    Task<Session> CreateAsync(Session session, CancellationToken ct = default);

    /// <summary>
    /// 根据 ID 查询会话
    /// </summary>
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 列出会话（分页）
    /// </summary>
    Task<PagedResult<Session>> ListAsync(int limit, int offset, CancellationToken ct = default);

    /// <summary>
    /// 搜索会话（按标题模糊匹配）
    /// </summary>
    Task<List<Session>> SearchAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// 更新会话
    /// </summary>
    Task UpdateAsync(Session session, CancellationToken ct = default);

    /// <summary>
    /// 删除会话
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: 实现 IMessageRepository 接口**

创建 `v3/src/GeneralAgent.Core/Abstractions/IMessageRepository.cs`:

```csharp
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// 消息仓储接口
/// </summary>
public interface IMessageRepository
{
    /// <summary>
    /// 创建消息
    /// </summary>
    Task<Message> CreateAsync(Message message, CancellationToken ct = default);

    /// <summary>
    /// 根据 ID 查询消息
    /// </summary>
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 查询会话的所有消息
    /// </summary>
    Task<List<Message>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 查询会话的最近 N 条消息
    /// </summary>
    Task<List<Message>> GetRecentAsync(Guid sessionId, int limit, CancellationToken ct = default);

    /// <summary>
    /// 统计会话的消息数量
    /// </summary>
    Task<int> CountAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// 删除会话的所有消息
    /// </summary>
    Task DeleteBySessionAsync(Guid sessionId, CancellationToken ct = default);
}
```

- [ ] **Step 3: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Core/GeneralAgent.Core.csproj
```

预期: 编译成功

- [ ] **Step 4: 提交接口定义**

```bash
git add v3/src/GeneralAgent.Core/Abstractions/
git commit -m "feat(v3-core): 定义 Repository 接口"
```

---

## Chunk 3: 存储层实现（EF Core + SQLite）

### Task 8: 创建 Infrastructure 项目和 DbContext

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj`
- Create: `v3/src/GeneralAgent.Infrastructure/Storage/AgentDbContext.cs`

- [ ] **Step 1: 创建 Infrastructure 项目**

```bash
cd v3
mkdir -p src/GeneralAgent.Infrastructure/Storage
dotnet new classlib -n GeneralAgent.Infrastructure -o src/GeneralAgent.Infrastructure
dotnet sln add src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj
dotnet add src/GeneralAgent.Infrastructure reference src/GeneralAgent.Core
rm src/GeneralAgent.Infrastructure/Class1.cs
```

- [ ] **Step 2: 添加 EF Core 依赖**

编辑 `v3/src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj`，添加包引用:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
</ItemGroup>
```

- [ ] **Step 3: 实现 AgentDbContext**

创建 `v3/src/GeneralAgent.Infrastructure/Storage/AgentDbContext.cs`:

```csharp
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage;

/// <summary>
/// Agent 数据库上下文
/// </summary>
public sealed class AgentDbContext : DbContext
{
    public AgentDbContext(DbContextOptions<AgentDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// 会话集合
    /// </summary>
    public DbSet<Session> Sessions => Set<Session>();

    /// <summary>
    /// 消息集合
    /// </summary>
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 应用所有配置
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentDbContext).Assembly);
    }
}
```

- [ ] **Step 4: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj
```

预期: 编译成功

- [ ] **Step 5: 提交 DbContext**

```bash
git add v3/src/GeneralAgent.Infrastructure/
git commit -m "feat(v3-infra): 创建 Infrastructure 项目和 DbContext"
```

**注意**: 数据库迁移步骤在 Task 9（实体配置完成后）执行。

---

### Task 9: 实现 EF Core 实体配置

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure/Storage/Configurations/SessionConfiguration.cs`
- Create: `v3/src/GeneralAgent.Infrastructure/Storage/Configurations/MessageConfiguration.cs`

- [ ] **Step 1: 实现 SessionConfiguration**

创建 `v3/src/GeneralAgent.Infrastructure/Storage/Configurations/SessionConfiguration.cs`:

```csharp
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// Session 实体配置
/// </summary>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .HasMaxLength(500);

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .IsRequired();

        builder.Property(s => s.ParentId);

        // 索引
        builder.HasIndex(s => s.CreatedAt);
        builder.HasIndex(s => s.UpdatedAt);
        builder.HasIndex(s => s.ParentId);
    }
}
```

- [ ] **Step 2: 实现 MessageConfiguration**

创建 `v3/src/GeneralAgent.Infrastructure/Storage/Configurations/MessageConfiguration.cs`:

```csharp
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace GeneralAgent.Infrastructure.Storage.Configurations;

/// <summary>
/// Message 实体配置
/// </summary>
internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SessionId)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Metadata 存储为 JSON（使用 JsonElement 避免反序列化类型问题）
        builder.Property(m => m.Metadata)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v, (JsonSerializerOptions?)null));

        // 索引
        builder.HasIndex(m => m.SessionId);
        builder.HasIndex(m => m.CreatedAt);

        // 外键关系（级联删除）
        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj
```

预期: 编译成功

- [ ] **Step 4: 提交实体配置**

```bash
git add v3/src/GeneralAgent.Infrastructure/Storage/Configurations/
git commit -m "feat(v3-infra): 添加 EF Core 实体配置"
```

---

## Chunk 3 续: Repository 实现和数据库迁移

### Task 10: 创建数据库迁移

**前置条件**: Task 8 和 Task 9 完成（DbContext 和实体配置已实现）

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure/Migrations/YYYYMMDDHHMMSS_InitialCreate.cs`
- Create: `v3/src/GeneralAgent.Infrastructure/Migrations/AgentDbContextModelSnapshot.cs`

**重要**: 迁移需要一个启动项目来读取配置。我们将在 Task 13 创建 Console 应用后再执行迁移。此任务仅记录迁移步骤，实际执行在 Task 13 之后。

- [ ] **Step 1: 安装 EF Core 工具（如未安装）**

```bash
dotnet tool install --global dotnet-ef
# 或更新现有工具
dotnet tool update --global dotnet-ef
```

预期: 输出工具版本号

- [ ] **Step 2: 验证工具安装**

```bash
dotnet ef --version
```

预期: 输出 `Entity Framework Core .NET Command-line Tools 9.0.x`

- [ ] **Step 3: 创建迁移（占位步骤）**

**注意**: 此步骤将在 Task 13（创建 Console 应用）之后执行。

```bash
# 占位命令（稍后执行）
cd v3
dotnet ef migrations add InitialCreate \
  --project src/GeneralAgent.Infrastructure \
  --startup-project src/GeneralAgent.Hosts.Console \
  --output-dir Storage/Migrations
```

预期命令说明:
- `--project`: 包含 DbContext 的项目
- `--startup-project`: 提供配置和依赖注入的宿主项目
- `--output-dir`: 迁移文件输出目录

- [ ] **Step 4: 应用迁移（占位步骤）**

**注意**: 此步骤将在 Task 13 之后执行。

```bash
# 占位命令（稍后执行）
cd v3
dotnet ef database update \
  --project src/GeneralAgent.Infrastructure \
  --startup-project src/GeneralAgent.Hosts.Console
```

预期: 创建 `agent.db` 文件，包含 `sessions` 和 `messages` 表

- [ ] **Step 5: 验证数据库结构（占位步骤）**

```bash
# 占位命令（稍后执行）
sqlite3 agent.db ".schema"
```

预期输出:
```sql
CREATE TABLE sessions (
  Id TEXT NOT NULL PRIMARY KEY,
  Title TEXT,
  Type TEXT NOT NULL,
  Status TEXT NOT NULL,
  ParentId TEXT,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL
);
CREATE INDEX IX_sessions_CreatedAt ON sessions (CreatedAt);
CREATE INDEX IX_sessions_UpdatedAt ON sessions (UpdatedAt);
CREATE INDEX IX_sessions_ParentId ON sessions (ParentId);

CREATE TABLE messages (
  Id TEXT NOT NULL PRIMARY KEY,
  SessionId TEXT NOT NULL,
  Role TEXT NOT NULL,
  Content TEXT NOT NULL,
  CreatedAt TEXT NOT NULL,
  Metadata TEXT,
  FOREIGN KEY (SessionId) REFERENCES sessions (Id) ON DELETE CASCADE
);
CREATE INDEX IX_messages_SessionId ON messages (SessionId);
CREATE INDEX IX_messages_CreatedAt ON messages (CreatedAt);
```

---

### Task 11: 实现 SessionRepository（TDD）

**Files:**
- Create: `v3/tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj`
- Create: `v3/tests/GeneralAgent.Infrastructure.Tests/Storage/SessionRepositoryTests.cs`
- Create: `v3/src/GeneralAgent.Infrastructure/Storage/Repositories/SessionRepository.cs`

- [ ] **Step 1: 创建 Infrastructure 测试项目**

```bash
cd v3
mkdir -p tests/GeneralAgent.Infrastructure.Tests/Storage
dotnet new xunit -n GeneralAgent.Infrastructure.Tests -o tests/GeneralAgent.Infrastructure.Tests
dotnet sln add tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj
dotnet add tests/GeneralAgent.Infrastructure.Tests reference src/GeneralAgent.Core
dotnet add tests/GeneralAgent.Infrastructure.Tests reference src/GeneralAgent.Infrastructure
rm tests/GeneralAgent.Infrastructure.Tests/UnitTest1.cs
```

- [ ] **Step 2: 添加测试依赖**

编辑 `v3/tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="FluentAssertions" />
  <PackageReference Include="xunit" />
  <PackageReference Include="xunit.runner.visualstudio" />
  <PackageReference Include="coverlet.collector" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
</ItemGroup>
```

- [ ] **Step 3: 编写 SessionRepository 测试（失败的）**

创建 `v3/tests/GeneralAgent.Infrastructure.Tests/Storage/SessionRepositoryTests.cs`:

```csharp
using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Tests.Storage;

public class SessionRepositoryTests : IDisposable
{
    private readonly AgentDbContext _context;
    private readonly ISessionRepository _repository;

    public SessionRepositoryTests()
    {
        // 使用内存数据库
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _repository = new SessionRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistSession()
    {
        // Arrange
        var session = Session.Create(title: "Test Session");

        // Act
        var created = await _repository.CreateAsync(session);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().Be(session.Id);
        created.Title.Should().Be("Test Session");

        var retrieved = await _repository.GetByIdAsync(session.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Session");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ShouldReturnPagedResults()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            await _repository.CreateAsync(Session.Create(title: $"Session {i}"));
        }

        // Act
        var page1 = await _repository.ListAsync(limit: 10, offset: 0);
        var page2 = await _repository.ListAsync(limit: 10, offset: 10);

        // Assert
        page1.Total.Should().Be(15);
        page1.Items.Count.Should().Be(10);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();

        page2.Total.Should().Be(15);
        page2.Items.Count.Should().Be(5);
        page2.HasNextPage.Should().BeFalse();
        page2.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_ShouldFindMatchingSessions()
    {
        // Arrange
        await _repository.CreateAsync(Session.Create(title: "Project Alpha"));
        await _repository.CreateAsync(Session.Create(title: "Project Beta"));
        await _repository.CreateAsync(Session.Create(title: "Task Alpha"));

        // Act
        var results = await _repository.SearchAsync("Alpha", limit: 10);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(s => s.Title!.Contains("Alpha"));
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        var session = Session.Create(title: "Original");
        await _repository.CreateAsync(session);

        // Act
        var updated = session.WithTitle("Updated");
        await _repository.UpdateAsync(updated);

        // Assert
        var retrieved = await _repository.GetByIdAsync(session.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSession()
    {
        // Arrange
        var session = Session.Create(title: "To Delete");
        await _repository.CreateAsync(session);

        // Act
        await _repository.DeleteAsync(session.Id);

        // Assert
        var retrieved = await _repository.GetByIdAsync(session.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithSubagent_ShouldSetTypeCorrectly()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var subagent = Session.Create(title: "Subagent", parentId: parentId);

        // Act
        await _repository.CreateAsync(subagent);

        // Assert
        var retrieved = await _repository.GetByIdAsync(subagent.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Type.Should().Be(SessionType.Subagent);
        retrieved.ParentId.Should().Be(parentId);
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj
```

预期: 所有测试失败，报错 "SessionRepository type not found"

- [ ] **Step 5: 实现 SessionRepository**

创建 `v3/src/GeneralAgent.Infrastructure/Storage/Repositories/SessionRepository.cs`:

```csharp
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Common;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// Session 仓储实现
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly AgentDbContext _context;

    public SessionRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Session> CreateAsync(Session session, CancellationToken ct = default)
    {
        try
        {
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync(ct);
            return session;
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to create session: {ex.Message}", ex);
        }
    }

    public async Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get session by ID: {ex.Message}", ex);
        }
    }

    public async Task<PagedResult<Session>> ListAsync(int limit, int offset, CancellationToken ct = default)
    {
        try
        {
            var total = await _context.Sessions.CountAsync(ct);

            var items = await _context.Sessions
                .AsNoTracking()
                .OrderByDescending(s => s.UpdatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(ct);

            return new PagedResult<Session>(items, total, limit, offset);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to list sessions: {ex.Message}", ex);
        }
    }

    public async Task<List<Session>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        try
        {
            return await _context.Sessions
                .AsNoTracking()
                .Where(s => s.Title != null && s.Title.Contains(query))
                .OrderByDescending(s => s.UpdatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to search sessions: {ex.Message}", ex);
        }
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        try
        {
            _context.Sessions.Update(session);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to update session: {ex.Message}", ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var session = await _context.Sessions.FindAsync(new object[] { id }, ct);
            if (session != null)
            {
                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to delete session: {ex.Message}", ex);
        }
    }
}
```

- [ ] **Step 6: 运行测试验证通过**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj
```

预期: 所有测试通过（7/7）

- [ ] **Step 7: 提交 SessionRepository**

```bash
git add v3/
git commit -m "feat(v3-infra): 实现 SessionRepository（TDD）"
```

---

### Task 12: 实现 MessageRepository（TDD）

**Files:**
- Create: `v3/tests/GeneralAgent.Infrastructure.Tests/Storage/MessageRepositoryTests.cs`
- Create: `v3/src/GeneralAgent.Infrastructure/Storage/Repositories/MessageRepository.cs`

- [ ] **Step 1: 编写 MessageRepository 测试（失败的）**

创建 `v3/tests/GeneralAgent.Infrastructure.Tests/Storage/MessageRepositoryTests.cs`:

```csharp
using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Tests.Storage;

public class MessageRepositoryTests : IDisposable
{
    private readonly AgentDbContext _context;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly Guid _testSessionId;

    public MessageRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _sessionRepository = new SessionRepository(_context);
        _messageRepository = new MessageRepository(_context);

        // 创建测试会话
        var session = Session.Create(title: "Test Session");
        _sessionRepository.CreateAsync(session).Wait();
        _testSessionId = session.Id;
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistMessage()
    {
        // Arrange
        var message = Message.CreateUser(_testSessionId, "Hello");

        // Act
        var created = await _messageRepository.CreateAsync(message);

        // Assert
        created.Should().NotBeNull();
        created.Id.Should().Be(message.Id);
        created.Content.Should().Be("Hello");

        var retrieved = await _messageRepository.GetByIdAsync(message.Id);
        retrieved.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _messageRepository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySessionAsync_ShouldReturnAllMessages()
    {
        // Arrange
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.CreateAsync(Message.CreateAssistant(_testSessionId, "Response 1"));
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 2"));

        // Act
        var messages = await _messageRepository.GetBySessionAsync(_testSessionId);

        // Assert
        messages.Should().HaveCount(3);
        messages.Should().BeInAscendingOrder(m => m.CreatedAt);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnLimitedMessages()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, $"Message {i}"));
            await Task.Delay(10); // 确保时间戳不同
        }

        // Act
        var recent = await _messageRepository.GetRecentAsync(_testSessionId, limit: 5);

        // Assert
        recent.Should().HaveCount(5);
        recent.Should().BeInDescendingOrder(m => m.CreatedAt);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.CreateAsync(Message.CreateAssistant(_testSessionId, "Response 1"));

        // Act
        var count = await _messageRepository.CountAsync(_testSessionId);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task DeleteBySessionAsync_ShouldRemoveAllMessages()
    {
        // Arrange
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.CreateAsync(Message.CreateAssistant(_testSessionId, "Response 1"));

        // Act
        await _messageRepository.DeleteBySessionAsync(_testSessionId);

        // Assert
        var messages = await _messageRepository.GetBySessionAsync(_testSessionId);
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteBySessionAsync_WhenSessionDeleted_ShouldCascadeDelete()
    {
        // Arrange
        await _messageRepository.CreateAsync(Message.CreateUser(_testSessionId, "Message 1"));
        await _messageRepository.CreateAsync(Message.CreateAssistant(_testSessionId, "Response 1"));

        // Act
        await _sessionRepository.DeleteAsync(_testSessionId);

        // Assert
        var messages = await _messageRepository.GetBySessionAsync(_testSessionId);
        messages.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj --filter "FullyQualifiedName~MessageRepositoryTests"
```

预期: 所有测试失败，报错 "MessageRepository type not found"

- [ ] **Step 3: 实现 MessageRepository**

创建 `v3/src/GeneralAgent.Infrastructure/Storage/Repositories/MessageRepository.cs`:

```csharp
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GeneralAgent.Infrastructure.Storage.Repositories;

/// <summary>
/// Message 仓储实现
/// </summary>
public sealed class MessageRepository : IMessageRepository
{
    private readonly AgentDbContext _context;

    public MessageRepository(AgentDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Message> CreateAsync(Message message, CancellationToken ct = default)
    {
        try
        {
            _context.Messages.Add(message);
            await _context.SaveChangesAsync(ct);
            return message;
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to create message: {ex.Message}", ex);
        }
    }

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id, ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get message by ID: {ex.Message}", ex);
        }
    }

    public async Task<List<Message>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get messages by session: {ex.Message}", ex);
        }
    }

    public async Task<List<Message>> GetRecentAsync(Guid sessionId, int limit, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to get recent messages: {ex.Message}", ex);
        }
    }

    public async Task<int> CountAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            return await _context.Messages
                .Where(m => m.SessionId == sessionId)
                .CountAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to count messages: {ex.Message}", ex);
        }
    }

    public async Task DeleteBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            var messages = await _context.Messages
                .Where(m => m.SessionId == sessionId)
                .ToListAsync(ct);

            _context.Messages.RemoveRange(messages);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            throw new StorageException($"Failed to delete messages by session: {ex.Message}", ex);
        }
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj --filter "FullyQualifiedName~MessageRepositoryTests"
```

预期: 所有测试通过（7/7）

- [ ] **Step 5: 运行全部 Infrastructure 测试**

```bash
cd v3
dotnet test tests/GeneralAgent.Infrastructure.Tests/GeneralAgent.Infrastructure.Tests.csproj
```

预期: 所有测试通过（14/14 - Session 7 + Message 7）

- [ ] **Step 6: 提交 MessageRepository**

```bash
git add v3/
git commit -m "feat(v3-infra): 实现 MessageRepository（TDD）"
```

---

### Task 13: 实现依赖注入扩展

**Files:**
- Create: `v3/src/GeneralAgent.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: 实现 DependencyInjection 扩展类**

创建 `v3/src/GeneralAgent.Infrastructure/DependencyInjection.cs`:

```csharp
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Infrastructure.Storage;
using GeneralAgent.Infrastructure.Storage.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure;

/// <summary>
/// Infrastructure 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Infrastructure 服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">SQLite 连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // 注册 DbContext
        services.AddDbContext<AgentDbContext>(options =>
            options.UseSqlite(connectionString));

        // 注册 Repositories
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();

        return services;
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Infrastructure/GeneralAgent.Infrastructure.csproj
```

预期: 编译成功

- [ ] **Step 3: 提交依赖注入扩展**

```bash
git add v3/src/GeneralAgent.Infrastructure/DependencyInjection.cs
git commit -m "feat(v3-infra): 添加依赖注入扩展"
```

---

## Chunk 4: 验证和测试

### Task 14: 创建 Hosts.Console 验证应用

**Files:**
- Create: `v3/src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj`
- Create: `v3/src/GeneralAgent.Hosts.Console/Program.cs`
- Create: `v3/src/GeneralAgent.Hosts.Console/appsettings.json`

- [ ] **Step 1: 创建 Console 项目**

```bash
cd v3
mkdir -p src/GeneralAgent.Hosts.Console
dotnet new console -n GeneralAgent.Hosts.Console -o src/GeneralAgent.Hosts.Console
dotnet sln add src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj
dotnet add src/GeneralAgent.Hosts.Console reference src/GeneralAgent.Core
dotnet add src/GeneralAgent.Hosts.Console reference src/GeneralAgent.Infrastructure
```

- [ ] **Step 2: 添加依赖包**

编辑 `v3/src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting" />
  <PackageReference Include="Microsoft.Extensions.Configuration" />
  <PackageReference Include="Microsoft.Extensions.Logging" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" />
</ItemGroup>
```

- [ ] **Step 3: 创建 appsettings.json**

创建 `v3/src/GeneralAgent.Hosts.Console/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AgentDb": "Data Source=agent.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

- [ ] **Step 4: 配置文件复制**

编辑 `v3/src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj`，添加:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 5: 实现 Program.cs**

创建 `v3/src/GeneralAgent.Hosts.Console/Program.cs`:

```csharp
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
```

- [ ] **Step 6: 编译验证**

```bash
cd v3
dotnet build src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj
```

预期: 编译成功

- [ ] **Step 7: 提交 Console 应用**

```bash
git add v3/src/GeneralAgent.Hosts.Console/
git commit -m "feat(v3-hosts): 创建 Console 验证应用"
```

---

### Task 15: 执行数据库迁移（实际执行）

**前置条件**: Task 14 完成（Console 应用已创建）

- [ ] **Step 1: 创建初始迁移**

```bash
cd v3
dotnet ef migrations add InitialCreate \
  --project src/GeneralAgent.Infrastructure \
  --startup-project src/GeneralAgent.Hosts.Console \
  --output-dir Storage/Migrations
```

预期:
- 在 `v3/src/GeneralAgent.Infrastructure/Storage/Migrations/` 创建迁移文件
- 输出 "Done. To undo this action, use 'ef migrations remove'"

- [ ] **Step 2: 应用迁移**

```bash
cd v3
dotnet ef database update \
  --project src/GeneralAgent.Infrastructure \
  --startup-project src/GeneralAgent.Hosts.Console
```

预期:
- 创建 `v3/agent.db` 文件
- 输出 "Applying migration 'YYYYMMDDHHMMSS_InitialCreate'"
- 输出 "Done."

- [ ] **Step 3: 验证数据库结构**

```bash
cd v3
sqlite3 agent.db ".schema sessions"
sqlite3 agent.db ".schema messages"
```

预期输出 sessions 表:
```sql
CREATE TABLE sessions (
    Id TEXT NOT NULL CONSTRAINT PK_sessions PRIMARY KEY,
    Title TEXT NULL,
    Type TEXT NOT NULL,
    Status TEXT NOT NULL,
    ParentId TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE INDEX IX_sessions_CreatedAt ON sessions (CreatedAt);
CREATE INDEX IX_sessions_UpdatedAt ON sessions (UpdatedAt);
CREATE INDEX IX_sessions_ParentId ON sessions (ParentId);
```

预期输出 messages 表:
```sql
CREATE TABLE messages (
    Id TEXT NOT NULL CONSTRAINT PK_messages PRIMARY KEY,
    SessionId TEXT NOT NULL,
    Role TEXT NOT NULL,
    Content TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    Metadata TEXT NULL,
    CONSTRAINT FK_messages_sessions_SessionId FOREIGN KEY (SessionId)
        REFERENCES sessions (Id) ON DELETE CASCADE
);
CREATE INDEX IX_messages_SessionId ON messages (SessionId);
CREATE INDEX IX_messages_CreatedAt ON messages (CreatedAt);
```

- [ ] **Step 4: 提交迁移文件**

```bash
git add v3/src/GeneralAgent.Infrastructure/Storage/Migrations/
git add v3/.gitignore  # 如果添加了 agent.db 忽略规则
git commit -m "feat(v3-infra): 添加初始数据库迁移"
```

- [ ] **Step 5: 更新 .gitignore（忽略数据库文件）**

编辑 `v3/.gitignore`，添加:

```
*.db
*.db-shm
*.db-wal
```

---

### Task 16: 端到端验证

**前置条件**: Task 15 完成（数据库迁移已应用）

- [ ] **Step 1: 运行 Console 应用**

```bash
cd v3
dotnet run --project src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj
```

预期输出:
```
=== General Agent V3 - Phase 1 验证 ===

1. 创建会话...
   ✓ 会话已创建: <guid>

2. 添加消息...
   ✓ 用户消息已添加: <guid>
   ✓ 助手消息已添加: <guid>

3. 查询会话消息...
   ✓ 共有 2 条消息
     - [User] 你好，这是测试消息
     - [Assistant] 收到，这是响应消息

4. 更新会话标题...
   ✓ 会话标题已更新

5. 列出所有会话...
   ✓ 共有 1 个会话
     - 测试会话（已更新） (2 条消息)

6. 搜索会话...
   ✓ 找到 1 个匹配的会话

7. 测试级联删除（创建临时会话）...
   ✓ 临时会话有 1 条消息
   ✓ 删除会话后，消息数量: 0 (应为 0)

=== 所有验证通过 ✓ ===
```

- [ ] **Step 2: 使用 SQLite 客户端验证数据**

```bash
cd v3
sqlite3 agent.db "SELECT COUNT(*) FROM sessions;"
sqlite3 agent.db "SELECT COUNT(*) FROM messages;"
sqlite3 agent.db "SELECT Id, Title, Status FROM sessions LIMIT 5;"
```

预期: 显示实际的数据条目

- [ ] **Step 3: 验证通过日志**

检查日志输出，确认:
- 所有操作成功执行
- 没有错误或警告
- 级联删除正常工作

---

### Task 17: 测试覆盖率验证

**前置条件**: 所有测试通过

- [ ] **Step 1: 运行覆盖率测试**

```bash
cd v3
dotnet test \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

预期:
- 所有测试通过
- 生成覆盖率报告（`.xml` 格式）

- [ ] **Step 2: 安装 ReportGenerator（如未安装）**

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

- [ ] **Step 3: 生成 HTML 覆盖率报告**

```bash
cd v3
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html
```

预期: 在 `v3/TestResults/CoverageReport/` 生成 HTML 报告

- [ ] **Step 4: 打开覆盖率报告**

```bash
cd v3
open TestResults/CoverageReport/index.html
# 或在 Linux: xdg-open TestResults/CoverageReport/index.html
```

- [ ] **Step 5: 验证覆盖率 >= 80%**

检查报告中的覆盖率指标:
- **Line Coverage**: 应 >= 80%
- **Branch Coverage**: 应 >= 70%
- **Core 项目**: 应 >= 90%（简单模型）
- **Infrastructure 项目**: 应 >= 80%

如果覆盖率不足，识别未覆盖的代码并添加测试。

- [ ] **Step 6: 记录覆盖率结果**

创建 `v3/TestResults/coverage-summary.txt`:

```bash
cd v3
echo "Phase 1 测试覆盖率报告" > TestResults/coverage-summary.txt
echo "生成日期: $(date)" >> TestResults/coverage-summary.txt
echo "" >> TestResults/coverage-summary.txt
echo "核心指标:" >> TestResults/coverage-summary.txt
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/Temp" -reporttypes:TextSummary
cat TestResults/Temp/Summary.txt >> TestResults/coverage-summary.txt
rm -rf TestResults/Temp
```

- [ ] **Step 7: 提交覆盖率验证结果**

```bash
cd v3
git add TestResults/.gitkeep  # 保留目录结构
git commit -m "test(v3): 验证测试覆盖率 >= 80%"
```

**注意**: 不要提交 `.xml` 和 HTML 报告到 Git，只记录覆盖率是否达标。

---

## Phase 1 完成检查清单

在标记 Phase 1 为完成之前，验证以下所有项：

### 1. 项目结构
- [ ] 解决方案文件创建（`v3/GeneralAgent.sln`）
- [ ] 全局配置文件（`Directory.Build.props`, `Directory.Packages.props`）
- [ ] Core 项目创建且编译通过
- [ ] Infrastructure 项目创建且编译通过
- [ ] Console 宿主应用创建且可运行

### 2. 核心模型
- [ ] Session 模型（不可变 record）
- [ ] Message 模型（不可变 record）
- [ ] 枚举类型（SessionType, SessionStatus, MessageRole）
- [ ] 所有模型测试通过

### 3. 通用类型
- [ ] Result<T> 模式实现
- [ ] PagedResult<T> 实现
- [ ] 异常类型（AgentException, StorageException）
- [ ] 所有通用类型测试通过

### 4. Repository 接口和实现
- [ ] ISessionRepository 接口定义
- [ ] IMessageRepository 接口定义
- [ ] SessionRepository 实现（TDD）
- [ ] MessageRepository 实现（TDD）
- [ ] 所有 Repository 测试通过

### 5. 数据库
- [ ] DbContext 实现
- [ ] 实体配置（SessionConfiguration, MessageConfiguration）
- [ ] 数据库迁移创建并应用
- [ ] agent.db 文件生成且结构正确

### 6. 依赖注入
- [ ] DependencyInjection 扩展实现
- [ ] Console 应用正确配置 DI

### 7. 测试
- [ ] Core.Tests 项目（18 个测试）
- [ ] Infrastructure.Tests 项目（14 个测试）
- [ ] 所有测试通过（32/32）
- [ ] 测试覆盖率 >= 80%

### 8. 验证
- [ ] Console 应用运行成功
- [ ] 所有 CRUD 操作验证通过
- [ ] 级联删除验证通过
- [ ] 数据持久化验证通过

### 9. 文档
- [ ] 所有代码包含 XML 注释
- [ ] 提交信息清晰（遵循 conventional commits）
- [ ] Git 历史清晰（每个 Task 一次提交）

### 10. Git 提交
- [ ] 至少 15 次提交（每个 Task 至少 1 次）
- [ ] 提交信息格式正确（feat/test/fix）
- [ ] 所有文件已 staged 并提交

---

## 下一步：Phase 2 预览

Phase 1 完成后，Phase 2 将实现：

1. **Application 层**
   - SessionService（会话业务逻辑）
   - ConversationService（对话管理）
   - DTO 和 Mapper

2. **LLM 集成**
   - ILlmClient 接口
   - AnthropicClient 实现
   - 流式响应处理

3. **技能系统**
   - Skill 模型和加载器
   - 技能执行器
   - 参数验证

4. **MCP 集成**
   - MCP 协议实现
   - 工具调用
   - 安全策略

详细设计见: `docs/superpowers/specs/2026-03-16-v3-csharp-architecture-design.md`

---

## 故障排除

### 问题 1: 迁移创建失败

**症状**: `dotnet ef migrations add` 报错 "Unable to create object of type 'AgentDbContext'"

**解决方案**:
```bash
# 确保 Console 项目正确配置了连接字符串
cat v3/src/GeneralAgent.Hosts.Console/appsettings.json

# 确保 EF Core Design 包已添加
dotnet list src/GeneralAgent.Hosts.Console package
```

### 问题 2: 测试失败 "Database is locked"

**症状**: SQLite 内存数据库被锁定

**解决方案**:
```csharp
// 确保在 Dispose 中关闭连接
public void Dispose()
{
    _context.Database.CloseConnection();  // 先关闭连接
    _context.Dispose();                   // 再释放上下文
}
```

### 问题 3: 覆盖率过低

**症状**: 覆盖率报告显示 < 80%

**解决方案**:
1. 识别未覆盖的代码路径
2. 添加边界条件测试
3. 添加异常处理测试
4. 验证所有公共 API 都有测试

### 问题 4: Console 应用运行失败

**症状**: `System.InvalidOperationException: Connection string 'AgentDb' not found`

**解决方案**:
```bash
# 确保 appsettings.json 被复制到输出目录
ls v3/src/GeneralAgent.Hosts.Console/bin/Debug/net9.0/appsettings.json

# 检查项目文件配置
grep -A3 "appsettings.json" v3/src/GeneralAgent.Hosts.Console/GeneralAgent.Hosts.Console.csproj
```

---

**计划状态**: ✅ 完整（17 个 Task）
**预计完成时间**: 4-6 小时
**测试覆盖率目标**: >= 80%
**验收标准**: Console 应用成功运行并通过所有验证
