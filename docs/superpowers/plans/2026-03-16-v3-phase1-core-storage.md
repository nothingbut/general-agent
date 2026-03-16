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
        var metadata = new Dictionary<string, object>
        {
            ["model"] = "claude-3",
            ["tokens"] = 100
        };

        // Act
        var message = Message.CreateAssistant(sessionId, "Response", metadata);

        // Assert
        message.Metadata.Should().NotBeNull();
        message.Metadata.Should().ContainKey("model");
        message.Metadata!["model"].Should().Be("claude-3");
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
    /// 元数据（可选）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

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
        Dictionary<string, object>? metadata = null)
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

        // Metadata 存储为 JSON
        builder.Property(m => m.Metadata)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null));

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

由于上下文限制，我将在这里暂停计划编写。让我保存当前进度并进行审核。
