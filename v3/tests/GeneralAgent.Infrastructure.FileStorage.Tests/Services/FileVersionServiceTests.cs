using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Services;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Services;

/// <summary>
/// FileVersionService 单元测试
/// </summary>
[Collection("FileStorage Collection")]
public class FileVersionServiceTests : IDisposable
{
    private readonly FileStorageFixture _fixture;
    private readonly FileVersionService _versionService;
    private readonly string _ownerId = "owner-123";
    private readonly string _otherUserId = "user-456";

    public FileVersionServiceTests(FileStorageFixture fixture)
    {
        _fixture = fixture;

        // 创建 FileVersionService
        var serviceLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FileVersionService>();
        _versionService = new FileVersionService(
            _fixture.Repository,
            serviceLogger);
    }

    #region CreateNewVersionAsync Tests

    [Fact]
    public async Task CreateNewVersionAsync_应该成功创建新版本()
    {
        // Arrange
        var originalFile = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var newFilePath = Path.Combine(_fixture.TestRootDirectory, "v2.txt");

        // Act
        var newVersion = await _versionService.CreateNewVersionAsync(
            originalFile.Id,
            newFilePath,
            2048,
            _ownerId);

        // Assert
        newVersion.Should().NotBeNull();
        newVersion.Version.Should().Be(2);
        newVersion.ParentFileId.Should().Be(originalFile.Id);
        newVersion.IsLatest.Should().BeTrue();
        newVersion.OwnerId.Should().Be(_ownerId);
    }

    [Fact]
    public async Task CreateNewVersionAsync_应该标记旧版本为非最新()
    {
        // Arrange
        var originalFile = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var newFilePath = Path.Combine(_fixture.TestRootDirectory, "v2.txt");

        // Act
        await _versionService.CreateNewVersionAsync(
            originalFile.Id,
            newFilePath,
            2048,
            _ownerId);

        // Assert
        var oldVersion = await _fixture.Repository.GetByIdAsync(originalFile.Id);
        oldVersion.Should().NotBeNull();
        oldVersion!.IsLatest.Should().BeFalse();
    }

    [Fact]
    public async Task CreateNewVersionAsync_版本号应该递增()
    {
        // Arrange
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);

        // Act - 创建 v2
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);

        // Act - 创建 v3
        var v3 = await _versionService.CreateNewVersionAsync(
            v2.Id,
            Path.Combine(_fixture.TestRootDirectory, "v3.txt"),
            3072,
            _ownerId);

        // Assert
        v1.Version.Should().Be(1);
        v2.Version.Should().Be(2);
        v3.Version.Should().Be(3);
    }

    [Fact]
    public async Task CreateNewVersionAsync_父文件不存在应该抛出异常()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _versionService.CreateNewVersionAsync(
                nonExistentFileId,
                "new.txt",
                1024,
                _ownerId));
    }

    [Fact]
    public async Task CreateNewVersionAsync_非所有者创建应该抛出异常()
    {
        // Arrange
        var originalFile = await CreateTestFile(_ownerId, "v1.txt", 1024);

        // Act & Assert - 其他用户尝试创建版本
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _versionService.CreateNewVersionAsync(
                originalFile.Id,
                "new.txt",
                1024,
                _otherUserId));
    }

    #endregion

    #region GetVersionHistoryAsync Tests

    [Fact]
    public async Task GetVersionHistoryAsync_应该返回所有版本()
    {
        // Arrange - 创建 3 个版本
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);
        var v3 = await _versionService.CreateNewVersionAsync(
            v2.Id,
            Path.Combine(_fixture.TestRootDirectory, "v3.txt"),
            3072,
            _ownerId);

        // Act
        var versions = await _versionService.GetVersionHistoryAsync(v3.Id);

        // Assert
        versions.Should().HaveCount(3);
        versions.Should().Contain(f => f.Id == v1.Id && f.Version == 1);
        versions.Should().Contain(f => f.Id == v2.Id && f.Version == 2);
        versions.Should().Contain(f => f.Id == v3.Id && f.Version == 3);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_应该从中间版本追溯到根()
    {
        // Arrange - 创建 3 个版本
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);
        var v3 = await _versionService.CreateNewVersionAsync(
            v2.Id,
            Path.Combine(_fixture.TestRootDirectory, "v3.txt"),
            3072,
            _ownerId);

        // Act - 从 v2 查询版本历史
        var versions = await _versionService.GetVersionHistoryAsync(v2.Id);

        // Assert - 应该返回所有 3 个版本
        versions.Should().HaveCount(3);
        versions.Should().Contain(f => f.Id == v1.Id);
        versions.Should().Contain(f => f.Id == v2.Id);
        versions.Should().Contain(f => f.Id == v3.Id);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_文件不存在应该抛出异常()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _versionService.GetVersionHistoryAsync(nonExistentFileId));
    }

    [Fact]
    public async Task GetVersionHistoryAsync_单个版本应该只返回自己()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, "single.txt", 1024);

        // Act
        var versions = await _versionService.GetVersionHistoryAsync(file.Id);

        // Assert
        versions.Should().HaveCount(1);
        versions[0].Id.Should().Be(file.Id);
    }

    #endregion

    #region RestoreVersionAsync Tests

    [Fact]
    public async Task RestoreVersionAsync_应该成功恢复到旧版本()
    {
        // Arrange - 创建 3 个版本
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);
        var v3 = await _versionService.CreateNewVersionAsync(
            v2.Id,
            Path.Combine(_fixture.TestRootDirectory, "v3.txt"),
            3072,
            _ownerId);

        // Act - 恢复到 v1
        var restored = await _versionService.RestoreVersionAsync(v3.Id, 1, _ownerId);

        // Assert
        restored.Should().NotBeNull();
        restored.Version.Should().Be(4); // 新版本号
        restored.FilePath.Should().Be(v1.FilePath); // 指向 v1 的文件路径
        restored.FileSize.Should().Be(v1.FileSize);
        restored.IsLatest.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreVersionAsync_应该标记当前版本为非最新()
    {
        // Arrange
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);

        // Act - 恢复到 v1
        await _versionService.RestoreVersionAsync(v2.Id, 1, _ownerId);

        // Assert
        var oldLatest = await _fixture.Repository.GetByIdAsync(v2.Id);
        oldLatest.Should().NotBeNull();
        oldLatest!.IsLatest.Should().BeFalse();
    }

    [Fact]
    public async Task RestoreVersionAsync_版本不存在应该抛出异常()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, "file.txt", 1024);

        // Act & Assert - 恢复不存在的版本
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _versionService.RestoreVersionAsync(file.Id, 999, _ownerId));
    }

    [Fact]
    public async Task RestoreVersionAsync_非所有者恢复应该抛出异常()
    {
        // Arrange
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);

        // Act & Assert - 其他用户尝试恢复
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _versionService.RestoreVersionAsync(v2.Id, 1, _otherUserId));
    }

    #endregion

    #region GetLatestVersionAsync Tests

    [Fact]
    public async Task GetLatestVersionAsync_应该返回最新版本()
    {
        // Arrange - 创建 3 个版本
        var v1 = await CreateTestFile(_ownerId, "v1.txt", 1024);
        var v2 = await _versionService.CreateNewVersionAsync(
            v1.Id,
            Path.Combine(_fixture.TestRootDirectory, "v2.txt"),
            2048,
            _ownerId);
        var v3 = await _versionService.CreateNewVersionAsync(
            v2.Id,
            Path.Combine(_fixture.TestRootDirectory, "v3.txt"),
            3072,
            _ownerId);

        // Act
        var latest = await _versionService.GetLatestVersionAsync(v1.Id);

        // Assert
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(v3.Id);
        latest.Version.Should().Be(3);
        latest.IsLatest.Should().BeTrue();
    }

    [Fact]
    public async Task GetLatestVersionAsync_单个版本应该返回自己()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, "file.txt", 1024);

        // Act
        var latest = await _versionService.GetLatestVersionAsync(file.Id);

        // Assert
        latest.Should().NotBeNull();
        latest!.Id.Should().Be(file.Id);
    }

    [Fact]
    public async Task GetLatestVersionAsync_文件不存在应该返回null()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var latest = await _versionService.GetLatestVersionAsync(nonExistentFileId);

        // Assert
        latest.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 创建测试文件
    /// </summary>
    private async Task<UploadedFile> CreateTestFile(
        string ownerId,
        string fileName,
        long fileSize)
    {
        var file = UploadedFile.Create(
            sessionId: Guid.NewGuid().ToString(),
            fileName: fileName,
            filePath: Path.Combine(_fixture.TestRootDirectory, fileName),
            fileType: Path.GetExtension(fileName),
            fileSize: fileSize,
            ownerId: ownerId,
            summary: $"Test file: {fileName}",
            tags: "test",
            accessLevel: FileAccessLevel.Private);

        return await _fixture.Repository.SaveAsync(file);
    }

    #endregion

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
