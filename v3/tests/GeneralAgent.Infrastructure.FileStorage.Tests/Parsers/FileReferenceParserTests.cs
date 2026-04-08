using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Parsers;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using GeneralAgent.Infrastructure.FileStorage.Tests.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Parsers;

/// <summary>
/// FileReferenceParser 单元测试
/// </summary>
[Collection("FileStorage Collection")]
public class FileReferenceParserTests : IDisposable
{
    private readonly FileReferenceParser _parser;
    private readonly FileStorageFixture _fixture;
    private readonly ILogger<FileReferenceParser> _logger;
    private readonly string _sessionId;
    private readonly string _ownerId = "test-user-123";

    public FileReferenceParserTests(FileStorageFixture fixture)
    {
        _fixture = fixture;
        _logger = Substitute.For<ILogger<FileReferenceParser>>();
        _parser = new FileReferenceParser(_fixture.StorageService, _logger);
        _sessionId = Guid.NewGuid().ToString();
    }

    [Fact]
    public void ExtractReferences_应该提取文件名引用()
    {
        // Arrange
        var message = "请查看 @file:config.json 文件";

        // Act
        var references = _parser.ExtractReferences(message);

        // Assert
        references.Should().HaveCount(1);
        references[0].FileName.Should().Be("config.json");
        references[0].IsIdReference.Should().BeFalse();
        references[0].OriginalText.Should().Be("@file:config.json");
        references[0].StartIndex.Should().Be(4);
    }

    [Fact]
    public void ExtractReferences_应该提取GUID引用()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var message = $"请查看 @file:{fileId} 文件";

        // Act
        var references = _parser.ExtractReferences(message);

        // Assert
        references.Should().HaveCount(1);
        references[0].FileId.Should().Be(fileId);
        references[0].IsIdReference.Should().BeTrue();
        references[0].OriginalText.Should().Be($"@file:{fileId}");
    }

    [Fact]
    public void ExtractReferences_应该提取多个文件引用()
    {
        // Arrange
        var message = "请查看 @file:config.json 和 @file:data.txt 这两个文件";

        // Act
        var references = _parser.ExtractReferences(message);

        // Assert
        references.Should().HaveCount(2);
        references[0].FileName.Should().Be("config.json");
        references[1].FileName.Should().Be("data.txt");
    }

    [Fact]
    public void ExtractReferences_应该支持带下划线和短横线的文件名()
    {
        // Arrange
        var message = "@file:my_file-name.json";

        // Act
        var references = _parser.ExtractReferences(message);

        // Assert
        references.Should().HaveCount(1);
        references[0].FileName.Should().Be("my_file-name.json");
    }

    [Fact]
    public void ExtractReferences_空消息应该返回空列表()
    {
        // Arrange
        var message = "这是一条普通消息，没有文件引用";

        // Act
        var references = _parser.ExtractReferences(message);

        // Assert
        references.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessageAsync_无引用时应该返回原消息()
    {
        // Arrange
        var message = "这是一条普通消息";

        // Act
        var result = await _parser.ProcessMessageAsync(message, _sessionId);

        // Assert
        result.OriginalMessage.Should().Be(message);
        result.ProcessedContent.Should().Be(message);
        result.HasFileReferences.Should().BeFalse();
        result.ResolvedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessMessageAsync_应该替换已解析的文件引用()
    {
        // Arrange
        var content = "这是测试文件的内容";
        var filePath = TestFileHelper.CreateTempTextFile(content);
        var uploadedFile = await _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId);

        var message = $"请查看 @file:{uploadedFile.Id}";

        // Act
        var result = await _parser.ProcessMessageAsync(message, _sessionId);

        // Assert
        result.HasFileReferences.Should().BeTrue();
        result.ResolvedFiles.Should().HaveCount(1);
        result.ResolvedFiles[0].IsResolved.Should().BeTrue();
        result.ProcessedContent.Should().Contain($"<file name=\"{uploadedFile.FileName}\"");
        result.ProcessedContent.Should().Contain(content);
        result.ProcessedContent.Should().Contain("</file>");
    }

    [Fact]
    public async Task ProcessMessageAsync_文件不存在时应该保持原引用()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var message = $"请查看 @file:{nonExistentId}";

        // Act
        var result = await _parser.ProcessMessageAsync(message, _sessionId);

        // Assert
        result.HasFileReferences.Should().BeTrue();
        result.ResolvedFiles.Should().HaveCount(1);
        result.ResolvedFiles[0].IsResolved.Should().BeFalse();
        result.ResolvedFiles[0].Error.Should().Contain("未找到");
        result.AllReferencesResolved.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessMessageAsync_应该处理截断的文件内容()
    {
        // Arrange
        var largeContent = new string('A', 15000); // 超过默认10000字符限制
        var filePath = TestFileHelper.CreateTempTextFile(largeContent);
        var uploadedFile = await _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId);

        var message = $"@file:{uploadedFile.Id}";

        // Act
        var result = await _parser.ProcessMessageAsync(message, _sessionId);

        // Assert
        result.ProcessedContent.Should().Contain("[内容已截断: 原始 15000 字符");
    }

    [Fact]
    public async Task ProcessMessageAsync_应该处理多个文件引用的顺序替换()
    {
        // Arrange
        var file1Path = TestFileHelper.CreateTempTextFile("内容1");
        var file2Path = TestFileHelper.CreateTempTextFile("内容2");

        var file1 = await _fixture.StorageService.UploadFileAsync(file1Path, _sessionId, _ownerId);
        var file2 = await _fixture.StorageService.UploadFileAsync(file2Path, _sessionId, _ownerId);

        var message = $"文件1: @file:{file1.Id} 和文件2: @file:{file2.Id}";

        // Act
        var result = await _parser.ProcessMessageAsync(message, _sessionId);

        // Assert
        result.ResolvedFiles.Should().HaveCount(2);
        result.AllReferencesResolved.Should().BeTrue();
        result.ProcessedContent.Should().Contain(file1.FileName);
        result.ProcessedContent.Should().Contain(file2.FileName);
        result.ProcessedContent.Should().Contain("内容1");
        result.ProcessedContent.Should().Contain("内容2");
    }

    public void Dispose()
    {
        // 临时文件会在测试结束后自动清理
        // TestFileHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
