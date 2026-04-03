using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Parsers;
using GeneralAgent.Infrastructure.FileStorage.Processors;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Parsers;

/// <summary>
/// FileReferenceParser 单元测试
/// </summary>
public class FileReferenceParserTests
{
    private readonly FileReferenceParser _parser;
    private readonly FileStorageService _fileStorage;
    private readonly FileRepository _mockRepository;
    private readonly FileProcessorService _mockProcessorService;
    private readonly ILogger<FileReferenceParser> _logger;

    public FileReferenceParserTests()
    {
        _mockRepository = Substitute.For<FileRepository>();
        _mockProcessorService = Substitute.For<FileProcessorService>();
        _logger = Substitute.For<ILogger<FileReferenceParser>>();

        var options = Options.Create(new FileStorageOptions());
        var storageLogger = Substitute.For<ILogger<FileStorageService>>();

        _fileStorage = new FileStorageService(
            options,
            _mockRepository,
            _mockProcessorService,
            storageLogger);

        _parser = new FileReferenceParser(_fileStorage, _logger);
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
        var sessionId = "test-session";

        // Act
        var result = await _parser.ProcessMessageAsync(message, sessionId);

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
        var fileId = Guid.NewGuid();
        var message = $"请查看 @file:{fileId}";
        var sessionId = "test-session";

        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            SessionId = sessionId,
            FileName = "test.txt",
            FileType = ".txt",
            FileSize = 100,
            UploadedAt = DateTime.UtcNow
        };

        var processedContent = ProcessedFileContent.Create("文件内容");

        _mockRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns(uploadedFile);

        _mockProcessorService.ProcessFileAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(processedContent);

        // Act
        var result = await _parser.ProcessMessageAsync(message, sessionId);

        // Assert
        result.HasFileReferences.Should().BeTrue();
        result.ResolvedFiles.Should().HaveCount(1);
        result.ResolvedFiles[0].IsResolved.Should().BeTrue();
        result.ProcessedContent.Should().Contain("<file name=\"test.txt\"");
        result.ProcessedContent.Should().Contain("文件内容");
        result.ProcessedContent.Should().Contain("</file>");
    }

    [Fact]
    public async Task ProcessMessageAsync_文件不存在时应该保持原引用()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var message = $"请查看 @file:{fileId}";
        var sessionId = "test-session";

        _mockRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns((UploadedFile?)null);

        // Act
        var result = await _parser.ProcessMessageAsync(message, sessionId);

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
        var fileId = Guid.NewGuid();
        var message = $"@file:{fileId}";
        var sessionId = "test-session";

        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            SessionId = sessionId,
            FileName = "large.txt",
            FileType = ".txt",
            FileSize = 20000,
            UploadedAt = DateTime.UtcNow
        };

        var processedContent = ProcessedFileContent.CreateTruncated(
            "截断后的内容",
            20000);

        _mockRepository.GetByIdAsync(fileId, Arg.Any<CancellationToken>())
            .Returns(uploadedFile);

        _mockProcessorService.ProcessFileAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(processedContent);

        // Act
        var result = await _parser.ProcessMessageAsync(message, sessionId);

        // Assert
        result.ProcessedContent.Should().Contain("截断后的内容");
        result.ProcessedContent.Should().Contain("[内容已截断: 原始 20000 字符");
    }

    [Fact]
    public async Task ProcessMessageAsync_应该处理多个文件引用的顺序替换()
    {
        // Arrange
        var file1Id = Guid.NewGuid();
        var file2Id = Guid.NewGuid();
        var message = $"文件1: @file:{file1Id} 和文件2: @file:{file2Id}";
        var sessionId = "test-session";

        var file1 = new UploadedFile
        {
            Id = file1Id,
            SessionId = sessionId,
            FileName = "file1.txt",
            FileType = ".txt",
            FileSize = 10,
            UploadedAt = DateTime.UtcNow
        };

        var file2 = new UploadedFile
        {
            Id = file2Id,
            SessionId = sessionId,
            FileName = "file2.txt",
            FileType = ".txt",
            FileSize = 10,
            UploadedAt = DateTime.UtcNow
        };

        _mockRepository.GetByIdAsync(file1Id, Arg.Any<CancellationToken>())
            .Returns(file1);
        _mockRepository.GetByIdAsync(file2Id, Arg.Any<CancellationToken>())
            .Returns(file2);

        _mockProcessorService.ProcessFileAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(
                ProcessedFileContent.Create("内容1"),
                ProcessedFileContent.Create("内容2"));

        // Act
        var result = await _parser.ProcessMessageAsync(message, sessionId);

        // Assert
        result.ResolvedFiles.Should().HaveCount(2);
        result.AllReferencesResolved.Should().BeTrue();
        result.ProcessedContent.Should().Contain("file1.txt");
        result.ProcessedContent.Should().Contain("file2.txt");
        result.ProcessedContent.Should().Contain("内容1");
        result.ProcessedContent.Should().Contain("内容2");
    }
}
