using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using GeneralAgent.Infrastructure.FileStorage.Tests.Helpers;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Services;

/// <summary>
/// FileStorageService 集成测试
/// </summary>
[Collection("FileStorage Collection")]
public class FileStorageServiceTests : IDisposable
{
    private readonly FileStorageFixture _fixture;
    private readonly string _sessionId;
    private readonly string _ownerId = "test-user-123";

    public FileStorageServiceTests(FileStorageFixture fixture)
    {
        _fixture = fixture;
        _sessionId = Guid.NewGuid().ToString();
    }

    [Fact]
    public async Task UploadFileAsync_应该成功上传文件()
    {
        // Arrange
        var content = "测试文件内容";
        var filePath = TestFileHelper.CreateTempTextFile(content);

        // Act
        var uploadedFile = await _fixture.StorageService.UploadFileAsync(
            filePath,
            _sessionId,
            _ownerId);

        // Assert
        uploadedFile.Should().NotBeNull();
        uploadedFile.Id.Should().NotBeEmpty();
        uploadedFile.SessionId.Should().Be(_sessionId);
        uploadedFile.FileName.Should().EndWith(".txt");
        uploadedFile.FileType.Should().Be(".txt");
        uploadedFile.FileSize.Should().BeGreaterThan(0);
        uploadedFile.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UploadFileAsync_文件不存在应该抛出异常()
    {
        // Arrange
        var filePath = "/path/to/nonexistent/file.txt";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId));
    }

    [Fact]
    public async Task UploadFileAsync_不支持的文件类型应该抛出异常()
    {
        // Arrange
        var filePath = TestFileHelper.CreateTempFile("test.exe", "binary content");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId));
    }

    [Fact]
    public async Task ReadFileContentAsync_应该正确读取文件内容()
    {
        // Arrange
        var originalContent = "测试文件内容\n第二行";
        var filePath = TestFileHelper.CreateTempTextFile(originalContent);
        var uploadedFile = await _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId);

        // Act
        var processedContent = await _fixture.StorageService.ReadFileContentAsync(uploadedFile);

        // Assert
        processedContent.Should().NotBeNull();
        processedContent.Content.Should().Be(originalContent);
        processedContent.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public async Task ListFilesAsync_应该返回会话的所有文件()
    {
        // Arrange
        var file1Path = TestFileHelper.CreateTempTextFile("内容1");
        var file2Path = TestFileHelper.CreateTempTextFile("内容2");

        await _fixture.StorageService.UploadFileAsync(file1Path, _sessionId, _ownerId);
        await _fixture.StorageService.UploadFileAsync(file2Path, _sessionId, _ownerId);

        // Act
        var files = await _fixture.StorageService.ListFilesAsync(_sessionId);

        // Assert
        files.Should().HaveCount(2);
        files.Should().OnlyContain(f => f.SessionId == _sessionId);
    }

    [Fact]
    public async Task DeleteFileAsync_应该成功删除文件()
    {
        // Arrange
        var filePath = TestFileHelper.CreateTempTextFile("待删除的内容");
        var uploadedFile = await _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId);

        // Act
        var deleted = await _fixture.StorageService.DeleteFileAsync(uploadedFile.Id);

        // Assert
        deleted.Should().BeTrue();

        // 验证文件已被删除
        var retrievedFile = await _fixture.StorageService.GetFileAsync(uploadedFile.Id);
        retrievedFile.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFileAsync_不存在的文件应该返回false()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var deleted = await _fixture.StorageService.DeleteFileAsync(nonExistentId);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetFilesByNameAsync_应该返回匹配的文件()
    {
        // Arrange
        var fileName = "test-unique.txt";
        var filePath = TestFileHelper.CreateTempFile(fileName, "内容");
        await _fixture.StorageService.UploadFileAsync(filePath, _sessionId, _ownerId);

        // Act
        var files = await _fixture.StorageService.GetFilesByNameAsync(fileName, _sessionId);

        // Assert
        files.Should().HaveCount(1);
        files[0].FileName.Should().Contain("test-unique.txt");
    }

    public void Dispose()
    {
        // 临时文件会在测试结束后自动清理
        // TestFileHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
