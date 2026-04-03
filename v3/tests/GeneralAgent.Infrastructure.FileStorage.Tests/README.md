# GeneralAgent.Infrastructure.FileStorage.Tests

文件存储基础设施的测试项目。

## 📁 目录结构

```
GeneralAgent.Infrastructure.FileStorage.Tests/
├── Fixtures/                    # 测试固件
│   └── FileStorageFixture.cs   # 共享的测试环境（临时目录、数据库、服务实例）
├── Helpers/                     # 测试辅助类
│   └── TestFileHelper.cs       # 文件创建和清理工具
├── TestData/                    # 测试样本文件
│   ├── sample.txt
│   ├── sample.json
│   ├── sample.cs
│   └── sample.md
├── Parsers/                     # 解析器测试
│   └── FileReferenceParserTests.cs
├── Processors/                  # 处理器测试
│   └── TextFileProcessorTests.cs
├── Services/                    # 服务测试
│   └── FileStorageServiceTests.cs
└── Repositories/                # 仓储测试（待添加）
```

## 🧪 测试类型

### 1. 单元测试
- **FileReferenceParserTests**：文件引用解析逻辑
- **TextFileProcessorTests**：文本文件处理逻辑

### 2. 集成测试
- **FileStorageServiceTests**：完整的文件存储流程（使用真实的 SQLite 和文件系统）

## 🔧 测试固件

### FileStorageFixture
提供共享的测试环境，包括：
- 临时测试目录（自动清理）
- 测试用 SQLite 数据库（内存或文件）
- 预配置的服务实例
- 会话目录创建工具

**使用方式**：
```csharp
[Collection("FileStorage Collection")]
public class MyTests
{
    private readonly FileStorageFixture _fixture;

    public MyTests(FileStorageFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestSomething()
    {
        // 使用 _fixture.StorageService
        var result = await _fixture.StorageService.UploadFileAsync(...);
        // ...
    }
}
```

## 🛠️ 测试辅助工具

### TestFileHelper
提供便捷的测试文件创建和管理：

```csharp
// 创建临时文本文件
var filePath = TestFileHelper.CreateTempTextFile("内容");

// 创建临时 JSON 文件
var jsonPath = TestFileHelper.CreateTempJsonFile();

// 创建临时代码文件
var codePath = TestFileHelper.CreateTempCodeFile(".cs");

// 创建大文件（测试截断）
var largePath = TestFileHelper.CreateLargeFile(sizeInKB: 100);

// 从 TestData 目录复制文件
var samplePath = TestFileHelper.CopyTestDataFile("sample.txt");

// 清理临时文件
TestFileHelper.CleanupTempFiles();
```

## 📊 运行测试

### 运行所有测试
```bash
dotnet test
```

### 运行特定测试类
```bash
dotnet test --filter "FullyQualifiedName~FileReferenceParserTests"
```

### 查看详细输出
```bash
dotnet test --logger "console;verbosity=detailed"
```

### 生成覆盖率报告
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## 📝 编写测试的最佳实践

### 1. 使用 Arrange-Act-Assert 模式
```csharp
[Fact]
public async Task TestSomething()
{
    // Arrange - 准备测试数据
    var input = "test";

    // Act - 执行被测试的操作
    var result = await _service.DoSomething(input);

    // Assert - 验证结果
    result.Should().Be("expected");
}
```

### 2. 测试命名约定
- 方法名：`被测试的方法_测试场景_预期结果`
- 示例：`UploadFileAsync_文件不存在_应该抛出异常`

### 3. 使用 FluentAssertions
```csharp
// 更易读的断言
result.Should().NotBeNull();
result.Should().HaveCount(5);
result.Should().BeEquivalentTo(expected);
```

### 4. 清理资源
```csharp
public class MyTests : IDisposable
{
    public void Dispose()
    {
        TestFileHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
```

### 5. 使用 Theory 进行参数化测试
```csharp
[Theory]
[InlineData(".txt")]
[InlineData(".md")]
[InlineData(".json")]
public void CanProcess_应该支持多种文件类型(string extension)
{
    var result = _processor.CanProcess(extension);
    result.Should().BeTrue();
}
```

## 🎯 测试覆盖目标

- **总体覆盖率**: 80%+
- **关键路径**: 100%（上传、读取、删除、引用解析）
- **错误处理**: 完整覆盖（文件不存在、类型不支持、大小超限等）

## 📚 相关资源

- [xUnit 文档](https://xunit.net/)
- [FluentAssertions 文档](https://fluentassertions.com/)
- [NSubstitute 文档](https://nsubstitute.github.io/)
