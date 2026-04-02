# V3 Phase 3 实施计划 - Skills System

**项目**: General Agent V3 - 技能系统
**Phase**: Phase 3
**预计时间**: Week 5-6 (2 weeks)
**日期**: 2026-03-17
**状态**: 🚀 准备开始

---

## 📋 目标

实现完整的技能系统，支持加载、注册和执行 Markdown 格式的技能文件。

### 成功标准

- ✅ 支持加载 Markdown 技能文件（YAML frontmatter + 内容）
- ✅ 技能注册表和命名空间管理
- ✅ 技能执行器（参数绑定和模板渲染）
- ✅ 集成到 ConversationService
- ✅ 支持 `@skill` 和 `/skill` 调用语法
- ✅ 80%+ 测试覆盖率
- ✅ 完整的示例和文档

---

## 🏗️ 架构设计

### 技能文件格式

```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

你好 {user_name}！今天有什么我可以帮助你的吗？
```

### 核心组件

```
GeneralAgent.Infrastructure.Skills/
├── Models/
│   ├── Skill.cs              # 技能模型
│   ├── SkillParameter.cs     # 参数定义
│   └── SkillMetadata.cs      # 元数据
├── Parsers/
│   ├── ISkillParser.cs       # 解析器接口
│   └── MarkdownSkillParser.cs # Markdown 解析实现
├── Loaders/
│   ├── ISkillLoader.cs       # 加载器接口
│   └── FileSystemSkillLoader.cs # 文件系统加载
├── Registry/
│   ├── ISkillRegistry.cs     # 注册表接口
│   └── SkillRegistry.cs      # 注册表实现
├── Executors/
│   ├── ISkillExecutor.cs     # 执行器接口
│   └── SkillExecutor.cs      # 执行器实现
└── DependencyInjection.cs    # DI 扩展
```

---

## 📦 任务分解

### Chunk 1: 核心模型和解析器（Day 1-2）

#### Task 1: 创建 Skills 项目和核心模型

**时间**: 30 分钟

**步骤**:

- [ ] **Step 1: 创建项目**

```bash
cd v3/src
dotnet new classlib -n GeneralAgent.Infrastructure.Skills
dotnet sln ../GeneralAgent.sln add GeneralAgent.Infrastructure.Skills/GeneralAgent.Infrastructure.Skills.csproj
```

- [ ] **Step 2: 添加项目引用**

```bash
cd GeneralAgent.Infrastructure.Skills
dotnet add reference ../GeneralAgent.Core/GeneralAgent.Core.csproj
```

- [ ] **Step 3: 添加 NuGet 包**

编辑 `Directory.Packages.props`：
```xml
<PackageVersion Include="YamlDotNet" Version="15.3.0" />
<PackageVersion Include="Scriban" Version="5.9.1" />
```

编辑 `GeneralAgent.Infrastructure.Skills.csproj`：
```xml
<ItemGroup>
  <PackageReference Include="YamlDotNet" />
  <PackageReference Include="Scriban" />
</ItemGroup>
```

- [ ] **Step 4: 创建 Models/Skill.cs**

```csharp
using System.Collections.Generic;

namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能定义
/// </summary>
public sealed record Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Template { get; init; }
    public required IReadOnlyList<SkillParameter> Parameters { get; init; }
    public string? Namespace { get; init; }
    public Dictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// 完整技能名称（包含命名空间）
    /// 例如：personal:greeting
    /// </summary>
    public string FullName => string.IsNullOrEmpty(Namespace)
        ? Name
        : $"{Namespace}:{Name}";
}
```

- [ ] **Step 5: 创建 Models/SkillParameter.cs**

```csharp
namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能参数定义
/// </summary>
public sealed record SkillParameter
{
    public required string Name { get; init; }
    public required string Type { get; init; }  // string, int, bool, array
    public required bool Required { get; init; }
    public string? Description { get; init; }
    public object? DefaultValue { get; init; }

    /// <summary>
    /// 验证参数值
    /// </summary>
    public Result<object> Validate(object? value)
    {
        // 必填检查
        if (Required && value == null)
        {
            return Result<object>.Failure($"参数 '{Name}' 是必填的");
        }

        // 类型检查（简化版）
        if (value != null && !IsValidType(value))
        {
            return Result<object>.Failure(
                $"参数 '{Name}' 类型不匹配，期望 {Type}");
        }

        return Result<object>.Success(value ?? DefaultValue!);
    }

    private bool IsValidType(object value)
    {
        return Type.ToLower() switch
        {
            "string" => value is string,
            "int" => value is int or long,
            "bool" => value is bool,
            "array" => value is System.Collections.IEnumerable,
            _ => true
        };
    }
}
```

- [ ] **Step 6: 创建 Models/SkillMetadata.cs**

```csharp
namespace GeneralAgent.Infrastructure.Skills.Models;

/// <summary>
/// 技能元数据（从 YAML frontmatter 解析）
/// </summary>
public sealed record SkillMetadata
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public List<SkillParameterMetadata> Parameters { get; init; } = new();
    public string? Namespace { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

public sealed record SkillParameterMetadata
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool Required { get; init; }
    public string? Description { get; init; }
    public object? DefaultValue { get; init; }
}
```

**验收标准**:
- ✅ 项目创建成功，可以编译
- ✅ 核心模型定义完整
- ✅ Skill.FullName 属性正确计算

---

#### Task 2: 实现 Markdown 技能解析器（TDD）

**时间**: 1.5 小时

**步骤**:

- [ ] **Step 1: 创建测试项目**

```bash
cd v3/tests
dotnet new xunit -n GeneralAgent.Infrastructure.Skills.Tests
dotnet sln ../GeneralAgent.sln add GeneralAgent.Infrastructure.Skills.Tests/GeneralAgent.Infrastructure.Skills.Tests.csproj
cd GeneralAgent.Infrastructure.Skills.Tests
dotnet add reference ../../src/GeneralAgent.Infrastructure.Skills/GeneralAgent.Infrastructure.Skills.csproj
```

- [ ] **Step 2: 编写失败测试 - Parsers/MarkdownSkillParserTests.cs**

```csharp
using GeneralAgent.Infrastructure.Skills.Parsers;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Tests.Parsers;

public class MarkdownSkillParserTests
{
    private readonly MarkdownSkillParser _parser = new();

    [Fact]
    public void Parse_ValidMarkdown_ReturnsSkill()
    {
        // Arrange
        var markdown = """
            ---
            name: greeting
            description: 向用户问候
            parameters:
              - name: user_name
                type: string
                required: true
                description: 用户名称
            ---

            你好 {user_name}！今天有什么我可以帮助你的吗？
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("greeting", result.Value!.Name);
        Assert.Equal("向用户问候", result.Value.Description);
        Assert.Single(result.Value.Parameters);
        Assert.Equal("你好 {user_name}！今天有什么我可以帮助你的吗？",
            result.Value.Template.Trim());
    }

    [Fact]
    public void Parse_MissingFrontmatter_ReturnsFailure()
    {
        // Arrange
        var markdown = "这只是普通内容，没有 YAML frontmatter";

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("frontmatter", result.Error!.ToLower());
    }

    [Fact]
    public void Parse_InvalidYaml_ReturnsFailure()
    {
        // Arrange
        var markdown = """
            ---
            name: test
            parameters: [invalid yaml
            ---
            content
            """;

        // Act
        var result = _parser.Parse(markdown);

        // Assert
        Assert.False(result.IsSuccess);
    }
}
```

- [ ] **Step 3: 运行测试（应该失败）**

```bash
dotnet test
```

预期：编译错误（MarkdownSkillParser 不存在）

- [ ] **Step 4: 实现解析器接口**

创建 `Parsers/ISkillParser.cs`：
```csharp
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Parsers;

/// <summary>
/// 技能解析器接口
/// </summary>
public interface ISkillParser
{
    /// <summary>
    /// 解析技能文件内容
    /// </summary>
    Result<Skill> Parse(string content);
}
```

- [ ] **Step 5: 实现 Markdown 解析器**

创建 `Parsers/MarkdownSkillParser.cs`：
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GeneralAgent.Infrastructure.Skills.Parsers;

/// <summary>
/// Markdown 技能解析器
/// 解析 YAML frontmatter + Markdown 内容
/// </summary>
public class MarkdownSkillParser : ISkillParser
{
    private static readonly Regex FrontmatterRegex = new(
        @"^---\s*\n(.*?)\n---\s*\n(.*)$",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly IDeserializer _yamlDeserializer;

    public MarkdownSkillParser()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public Result<Skill> Parse(string content)
    {
        try
        {
            // 提取 YAML frontmatter 和内容
            var match = FrontmatterRegex.Match(content);
            if (!match.Success)
            {
                return Result<Skill>.Failure(
                    "技能文件必须包含 YAML frontmatter（以 --- 包围）");
            }

            var yamlContent = match.Groups[1].Value;
            var templateContent = match.Groups[2].Value.Trim();

            // 解析 YAML
            var metadata = _yamlDeserializer.Deserialize<SkillMetadata>(yamlContent);

            // 验证必填字段
            if (string.IsNullOrWhiteSpace(metadata.Name))
            {
                return Result<Skill>.Failure("技能名称（name）不能为空");
            }

            if (string.IsNullOrWhiteSpace(metadata.Description))
            {
                return Result<Skill>.Failure("技能描述（description）不能为空");
            }

            // 转换参数
            var parameters = metadata.Parameters
                .Select(p => new SkillParameter
                {
                    Name = p.Name,
                    Type = p.Type,
                    Required = p.Required,
                    Description = p.Description,
                    DefaultValue = p.DefaultValue
                })
                .ToList();

            // 构建技能对象
            var skill = new Skill
            {
                Name = metadata.Name,
                Description = metadata.Description,
                Template = templateContent,
                Parameters = parameters,
                Namespace = metadata.Namespace,
                Tags = metadata.Tags
            };

            return Result<Skill>.Success(skill);
        }
        catch (Exception ex)
        {
            return Result<Skill>.Failure($"解析技能文件失败: {ex.Message}");
        }
    }
}
```

- [ ] **Step 6: 运行测试（应该通过）**

```bash
dotnet test
```

预期：所有测试通过 ✅

**验收标准**:
- ✅ 可以解析包含 YAML frontmatter 的 Markdown
- ✅ 可以提取技能名称、描述、参数、模板
- ✅ 验证必填字段
- ✅ 处理解析错误

---

### Chunk 2: 加载器和注册表（Day 3-4）

#### Task 3: 实现文件系统技能加载器（TDD）

**时间**: 1 小时

**步骤**:

- [ ] **Step 1: 编写失败测试 - Loaders/FileSystemSkillLoaderTests.cs**

```csharp
using System.IO;
using GeneralAgent.Infrastructure.Skills.Loaders;
using Xunit;

namespace GeneralAgent.Infrastructure.Skills.Tests.Loaders;

public class FileSystemSkillLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemSkillLoader _loader;

    public FileSystemSkillLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _loader = new FileSystemSkillLoader();
    }

    [Fact]
    public async Task LoadFromDirectory_ValidSkills_ReturnsSkills()
    {
        // Arrange
        var skillContent = """
            ---
            name: test_skill
            description: 测试技能
            parameters: []
            ---
            测试内容
            """;

        var skillPath = Path.Combine(_tempDir, "test_skill.md");
        await File.WriteAllTextAsync(skillPath, skillContent);

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("test_skill", result.Value[0].Name);
    }

    [Fact]
    public async Task LoadFromDirectory_WithNamespace_SetsNamespace()
    {
        // Arrange
        var namespaceDir = Path.Combine(_tempDir, "personal");
        Directory.CreateDirectory(namespaceDir);

        var skillContent = """
            ---
            name: greeting
            description: 问候
            parameters: []
            ---
            你好！
            """;

        await File.WriteAllTextAsync(
            Path.Combine(namespaceDir, "greeting.md"),
            skillContent);

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("personal", result.Value[0].Namespace);
        Assert.Equal("personal:greeting", result.Value[0].FullName);
    }

    [Fact]
    public async Task LoadFromDirectory_InvalidSkill_ReturnsFailure()
    {
        // Arrange
        var invalidPath = Path.Combine(_tempDir, "invalid.md");
        await File.WriteAllTextAsync(invalidPath, "无效内容");

        // Act
        var result = await _loader.LoadFromDirectoryAsync(_tempDir);

        // Assert
        Assert.False(result.IsSuccess);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
```

- [ ] **Step 2: 运行测试（应该失败）**

- [ ] **Step 3: 实现加载器接口**

创建 `Loaders/ISkillLoader.cs`：
```csharp
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Loaders;

/// <summary>
/// 技能加载器接口
/// </summary>
public interface ISkillLoader
{
    /// <summary>
    /// 从目录加载所有技能文件
    /// </summary>
    Task<Result<IReadOnlyList<Skill>>> LoadFromDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: 实现文件系统加载器**

创建 `Loaders/FileSystemSkillLoader.cs`：
```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Parsers;

namespace GeneralAgent.Infrastructure.Skills.Loaders;

/// <summary>
/// 从文件系统加载技能文件
/// </summary>
public class FileSystemSkillLoader : ISkillLoader
{
    private readonly ISkillParser _parser;

    public FileSystemSkillLoader()
    {
        _parser = new MarkdownSkillParser();
    }

    public FileSystemSkillLoader(ISkillParser parser)
    {
        _parser = parser;
    }

    public async Task<Result<IReadOnlyList<Skill>>> LoadFromDirectoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return Result<IReadOnlyList<Skill>>.Failure(
                    $"目录不存在: {directory}");
            }

            var skills = new List<Skill>();
            var errors = new List<string>();

            // 递归查找所有 .md 文件
            var skillFiles = Directory.GetFiles(
                directory,
                "*.md",
                SearchOption.AllDirectories);

            foreach (var filePath in skillFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                var parseResult = _parser.Parse(content);

                if (parseResult.IsSuccess)
                {
                    var skill = parseResult.Value!;

                    // 从目录结构推断命名空间
                    var relativePath = Path.GetRelativePath(directory, filePath);
                    var dirName = Path.GetDirectoryName(relativePath);

                    if (!string.IsNullOrEmpty(dirName) &&
                        string.IsNullOrEmpty(skill.Namespace))
                    {
                        skill = skill with { Namespace = dirName };
                    }

                    skills.Add(skill);
                }
                else
                {
                    errors.Add($"{filePath}: {parseResult.Error}");
                }
            }

            if (errors.Any())
            {
                return Result<IReadOnlyList<Skill>>.Failure(
                    $"加载技能时发生错误:\n{string.Join("\n", errors)}");
            }

            return Result<IReadOnlyList<Skill>>.Success(skills);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Skill>>.Failure(
                $"加载技能失败: {ex.Message}");
        }
    }
}
```

- [ ] **Step 5: 运行测试（应该通过）**

**验收标准**:
- ✅ 可以加载目录下所有 .md 文件
- ✅ 自动从目录结构推断命名空间
- ✅ 处理无效文件并报告错误

---

#### Task 4: 实现技能注册表（TDD）

**时间**: 1 小时

**步骤**:

- [ ] **Step 1: 编写失败测试 - Registry/SkillRegistryTests.cs**

```csharp
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Registry;
using Xunit;

namespace GeneralAgent.Infrastructure.Skills.Tests.Registry;

public class SkillRegistryTests
{
    [Fact]
    public void Register_NewSkill_AddsToRegistry()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = CreateTestSkill("test");

        // Act
        var result = registry.Register(skill);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(registry.Contains("test"));
    }

    [Fact]
    public void Register_DuplicateName_ReturnsFailure()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill1 = CreateTestSkill("test");
        var skill2 = CreateTestSkill("test");

        registry.Register(skill1);

        // Act
        var result = registry.Register(skill2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("已存在", result.Error!);
    }

    [Fact]
    public void Get_RegisteredSkill_ReturnsSkill()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = CreateTestSkill("test", "personal");
        registry.Register(skill);

        // Act - 测试不同的查找方式
        var result1 = registry.Get("test");
        var result2 = registry.Get("personal:test");

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(skill.Name, result1.Value!.Name);
        Assert.Equal(skill.Name, result2.Value!.Name);
    }

    [Fact]
    public void List_WithNamespaceFilter_ReturnsFilteredSkills()
    {
        // Arrange
        var registry = new SkillRegistry();
        registry.Register(CreateTestSkill("skill1", "personal"));
        registry.Register(CreateTestSkill("skill2", "personal"));
        registry.Register(CreateTestSkill("skill3", "work"));

        // Act
        var personalSkills = registry.List("personal");

        // Assert
        Assert.Equal(2, personalSkills.Count);
        Assert.All(personalSkills, s => Assert.Equal("personal", s.Namespace));
    }

    private static Skill CreateTestSkill(string name, string? ns = null)
    {
        return new Skill
        {
            Name = name,
            Description = "Test skill",
            Template = "Test template",
            Parameters = Array.Empty<SkillParameter>(),
            Namespace = ns
        };
    }
}
```

- [ ] **Step 2: 实现注册表接口**

创建 `Registry/ISkillRegistry.cs`：
```csharp
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Registry;

/// <summary>
/// 技能注册表接口
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// 注册技能
    /// </summary>
    Result<Unit> Register(Skill skill);

    /// <summary>
    /// 获取技能（支持 name 或 namespace:name）
    /// </summary>
    Result<Skill> Get(string nameOrFullName);

    /// <summary>
    /// 检查技能是否存在
    /// </summary>
    bool Contains(string nameOrFullName);

    /// <summary>
    /// 列出所有技能（可选按命名空间过滤）
    /// </summary>
    IReadOnlyList<Skill> List(string? namespaceFilter = null);

    /// <summary>
    /// 清空注册表
    /// </summary>
    void Clear();
}
```

- [ ] **Step 3: 实现注册表**

创建 `Registry/SkillRegistry.cs`：
```csharp
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Models;

namespace GeneralAgent.Infrastructure.Skills.Registry;

/// <summary>
/// 技能注册表实现
/// 线程安全，支持按名称和完整名称查找
/// </summary>
public class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, Skill> _skills = new();

    public Result<Unit> Register(Skill skill)
    {
        // 使用 FullName 作为键，确保唯一性
        if (!_skills.TryAdd(skill.FullName, skill))
        {
            return Result<Unit>.Failure($"技能 '{skill.FullName}' 已存在");
        }

        return Result<Unit>.Success(Unit.Value);
    }

    public Result<Skill> Get(string nameOrFullName)
    {
        // 先尝试直接查找
        if (_skills.TryGetValue(nameOrFullName, out var skill))
        {
            return Result<Skill>.Success(skill);
        }

        // 如果没有冒号，尝试按名称查找（忽略命名空间）
        if (!nameOrFullName.Contains(':'))
        {
            skill = _skills.Values.FirstOrDefault(s => s.Name == nameOrFullName);
            if (skill != null)
            {
                return Result<Skill>.Success(skill);
            }
        }

        return Result<Skill>.Failure($"技能 '{nameOrFullName}' 未找到");
    }

    public bool Contains(string nameOrFullName)
    {
        return Get(nameOrFullName).IsSuccess;
    }

    public IReadOnlyList<Skill> List(string? namespaceFilter = null)
    {
        var skills = _skills.Values;

        if (!string.IsNullOrEmpty(namespaceFilter))
        {
            skills = skills.Where(s => s.Namespace == namespaceFilter).ToList();
        }

        return skills.ToList();
    }

    public void Clear()
    {
        _skills.Clear();
    }
}
```

- [ ] **Step 4: 运行测试（应该通过）**

**验收标准**:
- ✅ 可以注册和查找技能
- ✅ 防止重复注册
- ✅ 支持按名称和完整名称查找
- ✅ 支持按命名空间过滤
- ✅ 线程安全

---

### Chunk 3: 执行器和模板渲染（Day 5-6）

#### Task 5: 实现技能执行器（TDD）

**时间**: 1.5 小时

**步骤**:

- [ ] **Step 1: 编写失败测试 - Executors/SkillExecutorTests.cs**

```csharp
using System.Collections.Generic;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Registry;
using Xunit;

namespace GeneralAgent.Infrastructure.Skills.Tests.Executors;

public class SkillExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ValidParameters_RendersTemplate()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new Skill
        {
            Name = "greeting",
            Description = "问候",
            Template = "你好 {{ user_name }}！",
            Parameters = new[]
            {
                new SkillParameter
                {
                    Name = "user_name",
                    Type = "string",
                    Required = true
                }
            }
        };

        registry.Register(skill);
        var executor = new SkillExecutor(registry);

        var parameters = new Dictionary<string, object>
        {
            ["user_name"] = "Alice"
        };

        // Act
        var result = await executor.ExecuteAsync("greeting", parameters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("你好 Alice！", result.Value);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRequiredParameter_ReturnsFailure()
    {
        // Arrange
        var registry = new SkillRegistry();
        var skill = new Skill
        {
            Name = "test",
            Description = "test",
            Template = "{{ required_param }}",
            Parameters = new[]
            {
                new SkillParameter
                {
                    Name = "required_param",
                    Type = "string",
                    Required = true
                }
            }
        };

        registry.Register(skill);
        var executor = new SkillExecutor(registry);

        // Act
        var result = await executor.ExecuteAsync("test", new Dictionary<string, object>());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("必填", result.Error!);
    }

    [Fact]
    public async Task ExecuteAsync_SkillNotFound_ReturnsFailure()
    {
        // Arrange
        var registry = new SkillRegistry();
        var executor = new SkillExecutor(registry);

        // Act
        var result = await executor.ExecuteAsync(
            "nonexistent",
            new Dictionary<string, object>());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("未找到", result.Error!);
    }
}
```

- [ ] **Step 2: 实现执行器接口**

创建 `Executors/ISkillExecutor.cs`：
```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Infrastructure.Skills.Executors;

/// <summary>
/// 技能执行器接口
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// 执行技能
    /// </summary>
    Task<Result<string>> ExecuteAsync(
        string nameOrFullName,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: 实现执行器**

创建 `Executors/SkillExecutor.cs`：
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Core.Models;
using GeneralAgent.Infrastructure.Skills.Registry;
using Scriban;

namespace GeneralAgent.Infrastructure.Skills.Executors;

/// <summary>
/// 技能执行器实现
/// 使用 Scriban 进行模板渲染
/// </summary>
public class SkillExecutor : ISkillExecutor
{
    private readonly ISkillRegistry _registry;

    public SkillExecutor(ISkillRegistry registry)
    {
        _registry = registry;
    }

    public async Task<Result<string>> ExecuteAsync(
        string nameOrFullName,
        Dictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 获取技能
            var skillResult = _registry.Get(nameOrFullName);
            if (!skillResult.IsSuccess)
            {
                return Result<string>.Failure(skillResult.Error!);
            }

            var skill = skillResult.Value!;

            // 验证参数
            var validatedParams = new Dictionary<string, object>();

            foreach (var param in skill.Parameters)
            {
                var hasValue = parameters.TryGetValue(param.Name, out var value);

                if (!hasValue)
                {
                    value = null;
                }

                var validateResult = param.Validate(value);
                if (!validateResult.IsSuccess)
                {
                    return Result<string>.Failure(validateResult.Error!);
                }

                validatedParams[param.Name] = validateResult.Value!;
            }

            // 渲染模板
            var template = Template.Parse(skill.Template);
            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages);
                return Result<string>.Failure($"模板解析错误: {errors}");
            }

            var rendered = await template.RenderAsync(
                validatedParams,
                member => member.Name);

            return Result<string>.Success(rendered);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"执行技能失败: {ex.Message}");
        }
    }
}
```

- [ ] **Step 4: 运行测试（应该通过）**

**验收标准**:
- ✅ 可以执行技能并渲染模板
- ✅ 验证必填参数
- ✅ 处理技能未找到的情况
- ✅ 处理模板渲染错误

---

### Chunk 4: 依赖注入和集成（Day 7-8）

#### Task 6: 实现依赖注入扩展

**时间**: 30 分钟

**步骤**:

- [ ] **Step 1: 创建 DependencyInjection.cs**

```csharp
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Parsers;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace GeneralAgent.Infrastructure.Skills;

/// <summary>
/// Skills 层依赖注入扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 添加 Skills 服务
    /// </summary>
    public static IServiceCollection AddSkillsInfrastructure(
        this IServiceCollection services,
        string? skillsDirectory = null)
    {
        // 注册核心服务
        services.AddSingleton<ISkillParser, MarkdownSkillParser>();
        services.AddSingleton<ISkillLoader, FileSystemSkillLoader>();
        services.AddSingleton<ISkillRegistry, SkillRegistry>();
        services.AddSingleton<ISkillExecutor, SkillExecutor>();

        // 如果提供了技能目录，启动时自动加载
        if (!string.IsNullOrEmpty(skillsDirectory))
        {
            services.AddHostedService(sp =>
                new SkillLoaderBackgroundService(
                    sp.GetRequiredService<ISkillLoader>(),
                    sp.GetRequiredService<ISkillRegistry>(),
                    skillsDirectory));
        }

        return services;
    }
}
```

- [ ] **Step 2: 创建后台加载服务**

创建 `SkillLoaderBackgroundService.cs`：
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.Skills;

/// <summary>
/// 后台技能加载服务
/// 应用启动时自动加载技能文件
/// </summary>
internal class SkillLoaderBackgroundService : BackgroundService
{
    private readonly ISkillLoader _loader;
    private readonly ISkillRegistry _registry;
    private readonly string _skillsDirectory;
    private readonly ILogger<SkillLoaderBackgroundService> _logger;

    public SkillLoaderBackgroundService(
        ISkillLoader loader,
        ISkillRegistry registry,
        string skillsDirectory,
        ILogger<SkillLoaderBackgroundService>? logger = null)
    {
        _loader = loader;
        _registry = registry;
        _skillsDirectory = skillsDirectory;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SkillLoaderBackgroundService>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("开始加载技能文件: {Directory}", _skillsDirectory);

            var result = await _loader.LoadFromDirectoryAsync(_skillsDirectory, stoppingToken);

            if (result.IsSuccess)
            {
                foreach (var skill in result.Value!)
                {
                    _registry.Register(skill);
                    _logger.LogDebug("已加载技能: {SkillName}", skill.FullName);
                }

                _logger.LogInformation("技能加载完成，共 {Count} 个技能", result.Value.Count);
            }
            else
            {
                _logger.LogError("加载技能失败: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "技能加载过程中发生错误");
        }
    }
}
```

**验收标准**:
- ✅ DI 扩展方法配置正确
- ✅ 启动时自动加载技能

---

#### Task 7: 集成到 Application 层

**时间**: 1 小时

**步骤**:

- [ ] **Step 1: 扩展 ConversationService**

编辑 `v3/src/GeneralAgent.Application/Services/ConversationService.cs`，添加技能调用支持：

```csharp
using GeneralAgent.Infrastructure.Skills.Executors;

public class ConversationService
{
    private readonly ISkillExecutor? _skillExecutor;

    // 在构造函数中添加可选依赖
    public ConversationService(
        // ... 其他参数 ...
        ISkillExecutor? skillExecutor = null)
    {
        // ...
        _skillExecutor = skillExecutor;
    }

    /// <summary>
    /// 检测并执行技能调用
    /// 支持 @skill 和 /skill 语法
    /// </summary>
    private async Task<string?> TryExecuteSkillAsync(
        string userMessage,
        CancellationToken cancellationToken)
    {
        if (_skillExecutor == null)
        {
            return null;
        }

        // 检测 @skill 或 /skill 语法
        if (!userMessage.StartsWith("@") && !userMessage.StartsWith("/"))
        {
            return null;
        }

        // 解析技能调用
        // 格式: @skill_name param1='value1' param2='value2'
        var parts = userMessage[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var skillName = parts[0];
        var parameters = new Dictionary<string, object>();

        // 简单的参数解析（实际实现可能需要更复杂的解析器）
        for (int i = 1; i < parts.Length; i++)
        {
            var keyValue = parts[i].Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0];
                var value = keyValue[1].Trim('\'', '"');
                parameters[key] = value;
            }
        }

        // 执行技能
        var result = await _skillExecutor.ExecuteAsync(skillName, parameters, cancellationToken);

        return result.IsSuccess ? result.Value : $"技能执行失败: {result.Error}";
    }

    // 在 SendMessageAsync 中使用
    public async Task<string> SendMessageAsync(
        Guid sessionId,
        string userMessage,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 尝试执行技能
        var skillResult = await TryExecuteSkillAsync(userMessage, cancellationToken);
        if (skillResult != null)
        {
            // 技能执行成功，保存消息
            await _messageRepository.CreateAsync(new Message
            {
                SessionId = sessionId,
                Role = MessageRole.User,
                Content = userMessage,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _messageRepository.CreateAsync(new Message
            {
                SessionId = sessionId,
                Role = MessageRole.Assistant,
                Content = skillResult,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            return skillResult;
        }

        // 2. 否则调用 LLM
        // ... 原有的 LLM 调用逻辑 ...
    }
}
```

- [ ] **Step 2: 更新 Application DI**

编辑 `v3/src/GeneralAgent.Application/DependencyInjection.cs`：

```csharp
public static IServiceCollection AddApplicationLayer(
    this IServiceCollection services,
    string? skillsDirectory = null)
{
    services.AddScoped<SessionService>();
    services.AddScoped<ConversationService>();

    // 如果提供了技能目录，添加 Skills 支持
    if (!string.IsNullOrEmpty(skillsDirectory))
    {
        services.AddSkillsInfrastructure(skillsDirectory);
    }

    return services;
}
```

- [ ] **Step 3: 创建示例技能文件**

在 Console 项目中创建 `skills/personal/greeting.md`：

```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
    description: 用户名称
---

你好 {{ user_name }}！今天有什么我可以帮助你的吗？
```

- [ ] **Step 4: 更新 Console Program.cs**

```csharp
// 添加 Skills 目录配置
var skillsDirectory = Path.Combine(
    AppDomain.CurrentDomain.BaseDirectory,
    "skills");

builder.Services.AddApplicationLayer(skillsDirectory);
```

**验收标准**:
- ✅ ConversationService 支持技能调用
- ✅ 示例技能可以正常执行
- ✅ Console 应用集成成功

---

### Chunk 5: 测试和文档（Day 9-10）

#### Task 8: 编写集成测试

**时间**: 1 小时

创建 `v3/tests/GeneralAgent.Integration.Tests/SkillSystemTests.cs`：

```csharp
using System.IO;
using System.Threading.Tasks;
using GeneralAgent.Infrastructure.Skills;
using GeneralAgent.Infrastructure.Skills.Executors;
using GeneralAgent.Infrastructure.Skills.Loaders;
using GeneralAgent.Infrastructure.Skills.Registry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GeneralAgent.Integration.Tests;

public class SkillSystemTests : IAsyncLifetime
{
    private readonly string _tempSkillsDir;
    private ServiceProvider? _serviceProvider;

    public SkillSystemTests()
    {
        _tempSkillsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempSkillsDir);
    }

    public async Task InitializeAsync()
    {
        // 创建测试技能文件
        var personalDir = Path.Combine(_tempSkillsDir, "personal");
        Directory.CreateDirectory(personalDir);

        await File.WriteAllTextAsync(
            Path.Combine(personalDir, "greeting.md"),
            """
            ---
            name: greeting
            description: 问候用户
            parameters:
              - name: user_name
                type: string
                required: true
            ---
            你好 {{ user_name }}！
            """);

        // 配置 DI
        var services = new ServiceCollection();
        services.AddSkillsInfrastructure(_tempSkillsDir);

        _serviceProvider = services.BuildServiceProvider();

        // 等待技能加载
        await Task.Delay(100);
    }

    [Fact]
    public async Task EndToEnd_LoadAndExecuteSkill_Success()
    {
        // Arrange
        var executor = _serviceProvider!.GetRequiredService<ISkillExecutor>();
        var registry = _serviceProvider.GetRequiredService<ISkillRegistry>();

        // Assert - 技能已加载
        Assert.True(registry.Contains("personal:greeting"));

        // Act - 执行技能
        var result = await executor.ExecuteAsync(
            "personal:greeting",
            new Dictionary<string, object> { ["user_name"] = "Alice" });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("你好 Alice！", result.Value);
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();

        if (Directory.Exists(_tempSkillsDir))
        {
            Directory.Delete(_tempSkillsDir, true);
        }

        await Task.CompletedTask;
    }
}
```

**验收标准**:
- ✅ 端到端测试通过
- ✅ 测试覆盖率 80%+

---

#### Task 9: 编写文档和验收

**时间**: 1 小时

- [ ] **Step 1: 创建 README**

创建 `v3/src/GeneralAgent.Infrastructure.Skills/README.md`：

```markdown
# GeneralAgent.Infrastructure.Skills

技能系统实现，支持加载和执行 Markdown 格式的技能文件。

## 功能特性

- ✅ Markdown 技能文件解析（YAML frontmatter）
- ✅ 文件系统加载器（支持命名空间）
- ✅ 技能注册表（线程安全）
- ✅ Scriban 模板引擎
- ✅ 参数验证和类型检查
- ✅ 依赖注入集成

## 快速开始

### 1. 添加依赖

```bash
dotnet add reference GeneralAgent.Infrastructure.Skills
```

### 2. 配置服务

```csharp
services.AddSkillsInfrastructure("./skills");
```

### 3. 创建技能文件

`skills/personal/greeting.md`:
```markdown
---
name: greeting
description: 向用户问候
parameters:
  - name: user_name
    type: string
    required: true
---

你好 {{ user_name }}！
```

### 4. 执行技能

```csharp
var executor = serviceProvider.GetRequiredService<ISkillExecutor>();

var result = await executor.ExecuteAsync(
    "personal:greeting",
    new Dictionary<string, object> { ["user_name"] = "Alice" });

Console.WriteLine(result.Value); // 输出：你好 Alice！
```

## 技能文件格式

### YAML Frontmatter

```yaml
---
name: skill_name          # 必填：技能名称
description: 描述         # 必填：技能描述
namespace: category       # 可选：命名空间（默认从目录推断）
parameters:               # 可选：参数列表
  - name: param_name      # 参数名称
    type: string          # 类型：string, int, bool, array
    required: true        # 是否必填
    description: 说明     # 参数说明
    default: value        # 默认值
tags:                     # 可选：标签
  category: tool
  version: "1.0"
---
```

### 模板语法

使用 [Scriban](https://github.com/scriban/scriban) 语法：

```liquid
{{ variable_name }}
{{ object.property }}
{% if condition %}...{% end %}
{% for item in items %}...{% end %}
```

## API 文档

详见代码注释和接口定义。
```

- [ ] **Step 2: 手动验收**

创建 `v3/MANUAL_ACCEPTANCE_PHASE3.md`：

```markdown
# Phase 3 手动验收清单

## 环境准备

1. 确保 Phase 2 已完成
2. 创建测试技能文件

## 验收步骤

### 1. 技能加载测试

**步骤**:
1. 启动 Console 应用
2. 检查日志输出是否显示"技能加载完成"

**预期结果**: ✅ 技能加载成功，无错误

### 2. 技能执行测试（@语法）

**输入**: `@greeting user_name='Alice'`

**预期输出**: `你好 Alice！今天有什么我可以帮助你的吗？`

### 3. 技能执行测试（/语法）

**输入**: `/greeting user_name='Bob'`

**预期输出**: `你好 Bob！今天有什么我可以帮助你的吗？`

### 4. 参数验证测试

**输入**: `@greeting` (缺少必填参数)

**预期输出**: `技能执行失败: 参数 'user_name' 是必填的`

### 5. 技能未找到测试

**输入**: `@nonexistent`

**预期输出**: `技能执行失败: 技能 'nonexistent' 未找到`

### 6. 命名空间测试

**输入**: `@personal:greeting user_name='Charlie'`

**预期输出**: `你好 Charlie！今天有什么我可以帮助你的吗？`

## 测试覆盖率

运行：
```bash
dotnet test --collect:"XPlat Code Coverage"
```

**预期**: 覆盖率 > 80%

## 性能测试

- [ ] 加载 100 个技能文件 < 1 秒
- [ ] 执行单个技能 < 10ms

## 完成标准

- [x] 所有单元测试通过
- [x] 所有集成测试通过
- [x] 手动验收全部通过
- [x] 测试覆盖率 > 80%
- [x] 文档完整
```

**验收标准**:
- ✅ README 文档完整
- ✅ 手动验收清单完整
- ✅ 所有验收项通过

---

## 📊 进度跟踪

| Chunk | 任务 | 预计时间 | 状态 |
|-------|------|---------|------|
| Chunk 1 | Task 1-2（模型和解析器） | Day 1-2 | ⏳ 待开始 |
| Chunk 2 | Task 3-4（加载器和注册表） | Day 3-4 | ⏳ 待开始 |
| Chunk 3 | Task 5（执行器） | Day 5-6 | ⏳ 待开始 |
| Chunk 4 | Task 6-7（DI 和集成） | Day 7-8 | ⏳ 待开始 |
| Chunk 5 | Task 8-9（测试和文档） | Day 9-10 | ⏳ 待开始 |

---

## ✅ 最终验收标准

### 功能完整性
- [x] 支持 Markdown 技能文件解析
- [x] 支持文件系统加载（递归，命名空间）
- [x] 支持技能注册和查找
- [x] 支持 Scriban 模板渲染
- [x] 支持参数验证
- [x] 集成到 ConversationService
- [x] 支持 @skill 和 /skill 语法

### 质量标准
- [x] 80%+ 单元测试覆盖率
- [x] 完整的集成测试
- [x] 手动验收通过
- [x] 文档完整（README + API）
- [x] 代码符合项目规范

---

**创建日期**: 2026-03-17
**预计完成**: 2026-03-31 (2 weeks)
**负责人**: 开发团队
