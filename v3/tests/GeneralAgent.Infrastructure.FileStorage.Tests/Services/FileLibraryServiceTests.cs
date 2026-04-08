using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Services;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Services;

/// <summary>
/// FileLibraryService 单元测试
/// </summary>
public class FileLibraryServiceTests : IAsyncLifetime
{
    private FileStorageFixture _fixture = null!;
    private FileLibraryService _libraryService = null!;
    private FilePermissionService _permissionService = null!;
    private IFilePermissionRepository _permissionRepository = null!;
    private readonly string _user1Id = "user-1";
    private readonly string _user2Id = "user-2";
    private readonly string _user3Id = "user-3";

    public async Task InitializeAsync()
    {
        // 为每个测试创建独立的 Fixture
        _fixture = new FileStorageFixture();

        // 创建 FilePermissionRepository
        var permRepoLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionRepository>();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_fixture.Options);
        _permissionRepository = new FilePermissionRepository(optionsWrapper, permRepoLogger);

        // 创建 FilePermissionService
        var permServiceLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionService>();
        _permissionService = new FilePermissionService(
            _permissionRepository,
            _fixture.Repository,
            permServiceLogger);

        // 创建 FileLibraryService
        var libraryServiceLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FileLibraryService>();
        _libraryService = new FileLibraryService(
            _fixture.Repository,
            _permissionRepository,
            _permissionService,
            libraryServiceLogger);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _fixture?.Dispose();
        await Task.CompletedTask;
    }

    #region ListAccessibleFilesAsync Tests

    [Fact]
    public async Task ListAccessibleFilesAsync_应该返回所有者文件加公开文件加共享文件()
    {
        // Arrange
        // User1 拥有的文件
        var user1Private = await CreateTestFile(_user1Id, FileAccessLevel.Private, "user1-private.txt");
        var user1Public = await CreateTestFile(_user1Id, FileAccessLevel.Public, "user1-public.txt");

        // User2 拥有的公开文件
        var user2Public = await CreateTestFile(_user2Id, FileAccessLevel.Public, "user2-public.txt");

        // User3 共享给 User1 的文件
        var user3Shared = await CreateTestFile(_user3Id, FileAccessLevel.Shared, "user3-shared.txt");
        await _permissionService.GrantPermissionAsync(
            user3Shared.Id, _user1Id, _user3Id, PermissionType.Read);

        // Act
        var accessibleFiles = await _libraryService.ListAccessibleFilesAsync(_user1Id);

        // Assert
        accessibleFiles.Should().HaveCount(4);
        accessibleFiles.Should().Contain(f => f.Id == user1Private.Id);
        accessibleFiles.Should().Contain(f => f.Id == user1Public.Id);
        accessibleFiles.Should().Contain(f => f.Id == user2Public.Id);
        accessibleFiles.Should().Contain(f => f.Id == user3Shared.Id);
    }

    [Fact]
    public async Task ListAccessibleFilesAsync_应该按访问级别过滤()
    {
        // Arrange
        var privateFile = await CreateTestFile(_user1Id, FileAccessLevel.Private, "private.txt");
        var sharedFile = await CreateTestFile(_user1Id, FileAccessLevel.Shared, "shared.txt");
        var publicFile = await CreateTestFile(_user1Id, FileAccessLevel.Public, "public.txt");

        // Act - 只查询私有文件
        var privateFiles = await _libraryService.ListAccessibleFilesAsync(
            _user1Id, FileAccessLevel.Private);

        // Assert
        privateFiles.Should().HaveCount(1);
        privateFiles[0].Id.Should().Be(privateFile.Id);
    }

    [Fact]
    public async Task ListAccessibleFilesAsync_应该去重并排序()
    {
        // Arrange - 创建多个文件，确保有时间差
        var file1 = await CreateTestFile(_user1Id, FileAccessLevel.Public, "file1.txt");
        await Task.Delay(100);
        var file2 = await CreateTestFile(_user1Id, FileAccessLevel.Public, "file2.txt");
        await Task.Delay(100);
        var file3 = await CreateTestFile(_user1Id, FileAccessLevel.Public, "file3.txt");

        // Act
        var files = await _libraryService.ListAccessibleFilesAsync(_user1Id);

        // Assert - 应该按上传时间倒序排列
        files.Should().HaveCount(3);
        files[0].Id.Should().Be(file3.Id); // 最新的在前
        files[1].Id.Should().Be(file2.Id);
        files[2].Id.Should().Be(file1.Id);
    }

    [Fact]
    public async Task ListAccessibleFilesAsync_不应该包含无权限的私有文件()
    {
        // Arrange
        var user2PrivateFile = await CreateTestFile(_user2Id, FileAccessLevel.Private, "user2-private.txt");

        // Act
        var accessibleFiles = await _libraryService.ListAccessibleFilesAsync(_user1Id);

        // Assert
        accessibleFiles.Should().NotContain(f => f.Id == user2PrivateFile.Id);
    }

    #endregion

    #region SearchFilesAsync Tests

    [Fact]
    public async Task SearchFilesAsync_应该按关键词搜索并过滤权限()
    {
        // Arrange
        // User1 的文件（可访问）
        var user1File = await CreateTestFile(_user1Id, FileAccessLevel.Private, "report-2024.txt");

        // User2 的公开文件（可访问）
        var user2File = await CreateTestFile(_user2Id, FileAccessLevel.Public, "report-summary.txt");

        // User3 的私有文件（不可访问）
        var user3File = await CreateTestFile(_user3Id, FileAccessLevel.Private, "report-confidential.txt");

        // Act
        var searchResults = await _libraryService.SearchFilesAsync(_user1Id, "report");

        // Assert
        searchResults.Should().HaveCount(2);
        searchResults.Should().Contain(f => f.Id == user1File.Id);
        searchResults.Should().Contain(f => f.Id == user2File.Id);
        searchResults.Should().NotContain(f => f.Id == user3File.Id);
    }

    [Fact]
    public async Task SearchFilesAsync_无匹配结果应该返回空列表()
    {
        // Arrange
        await CreateTestFile(_user1Id, FileAccessLevel.Public, "file.txt");

        // Act
        var searchResults = await _libraryService.SearchFilesAsync(_user1Id, "nonexistent");

        // Assert
        searchResults.Should().BeEmpty();
    }

    #endregion

    #region GetFileAsync Tests

    [Fact]
    public async Task GetFileAsync_有权限应该返回文件()
    {
        // Arrange
        var file = await CreateTestFile(_user1Id, FileAccessLevel.Private, "file.txt");

        // Act
        var retrievedFile = await _libraryService.GetFileAsync(file.Id, _user1Id);

        // Assert
        retrievedFile.Should().NotBeNull();
        retrievedFile!.Id.Should().Be(file.Id);
    }

    [Fact]
    public async Task GetFileAsync_无权限应该返回null()
    {
        // Arrange
        var file = await CreateTestFile(_user1Id, FileAccessLevel.Private, "file.txt");

        // Act - User2 尝试访问 User1 的私有文件
        var retrievedFile = await _libraryService.GetFileAsync(file.Id, _user2Id);

        // Assert
        retrievedFile.Should().BeNull();
    }

    [Fact]
    public async Task GetFileAsync_公开文件所有人可访问()
    {
        // Arrange
        var file = await CreateTestFile(_user1Id, FileAccessLevel.Public, "file.txt");

        // Act - User2 访问 User1 的公开文件
        var retrievedFile = await _libraryService.GetFileAsync(file.Id, _user2Id);

        // Assert
        retrievedFile.Should().NotBeNull();
        retrievedFile!.Id.Should().Be(file.Id);
    }

    #endregion

    #region ListOwnedFilesAsync Tests

    [Fact]
    public async Task ListOwnedFilesAsync_应该返回用户拥有的所有文件()
    {
        // Arrange
        var file1 = await CreateTestFile(_user1Id, FileAccessLevel.Private, "file1.txt");
        var file2 = await CreateTestFile(_user1Id, FileAccessLevel.Public, "file2.txt");
        var file3 = await CreateTestFile(_user2Id, FileAccessLevel.Public, "file3.txt");

        // Act
        var ownedFiles = await _libraryService.ListOwnedFilesAsync(_user1Id);

        // Assert
        ownedFiles.Should().HaveCount(2);
        ownedFiles.Should().Contain(f => f.Id == file1.Id);
        ownedFiles.Should().Contain(f => f.Id == file2.Id);
        ownedFiles.Should().NotContain(f => f.Id == file3.Id);
    }

    [Fact]
    public async Task ListOwnedFilesAsync_无文件应该返回空列表()
    {
        // Act
        var ownedFiles = await _libraryService.ListOwnedFilesAsync(_user1Id);

        // Assert
        ownedFiles.Should().BeEmpty();
    }

    #endregion

    #region ListSharedFilesAsync Tests

    [Fact]
    public async Task ListSharedFilesAsync_应该返回与用户共享的文件()
    {
        // Arrange
        var sharedFile1 = await CreateTestFile(_user2Id, FileAccessLevel.Shared, "shared1.txt");
        var sharedFile2 = await CreateTestFile(_user3Id, FileAccessLevel.Shared, "shared2.txt");

        // 授予 User1 权限
        await _permissionService.GrantPermissionAsync(
            sharedFile1.Id, _user1Id, _user2Id, PermissionType.Read);
        await _permissionService.GrantPermissionAsync(
            sharedFile2.Id, _user1Id, _user3Id, PermissionType.Write);

        // Act
        var sharedFiles = await _libraryService.ListSharedFilesAsync(_user1Id);

        // Assert
        sharedFiles.Should().HaveCount(2);
        sharedFiles.Should().Contain(f => f.Id == sharedFile1.Id);
        sharedFiles.Should().Contain(f => f.Id == sharedFile2.Id);
    }

    [Fact]
    public async Task ListSharedFilesAsync_无共享文件应该返回空列表()
    {
        // Act
        var sharedFiles = await _libraryService.ListSharedFilesAsync(_user1Id);

        // Assert
        sharedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSharedFilesAsync_不应该包含公开文件()
    {
        // Arrange
        var publicFile = await CreateTestFile(_user2Id, FileAccessLevel.Public, "public.txt");

        // 虽然公开，但不在 ListSharedFilesAsync 结果中
        // Act
        var sharedFiles = await _libraryService.ListSharedFilesAsync(_user1Id);

        // Assert
        sharedFiles.Should().NotContain(f => f.Id == publicFile.Id);
    }

    #endregion

    #region ListPublicFilesAsync Tests

    [Fact]
    public async Task ListPublicFilesAsync_应该返回所有公开文件()
    {
        // Arrange
        var publicFile1 = await CreateTestFile(_user1Id, FileAccessLevel.Public, "public1.txt");
        var publicFile2 = await CreateTestFile(_user2Id, FileAccessLevel.Public, "public2.txt");
        var privateFile = await CreateTestFile(_user1Id, FileAccessLevel.Private, "private.txt");

        // Act
        var publicFiles = await _libraryService.ListPublicFilesAsync();

        // Assert
        publicFiles.Should().HaveCount(2);
        publicFiles.Should().Contain(f => f.Id == publicFile1.Id);
        publicFiles.Should().Contain(f => f.Id == publicFile2.Id);
        publicFiles.Should().NotContain(f => f.Id == privateFile.Id);
    }

    [Fact]
    public async Task ListPublicFilesAsync_无公开文件应该返回空列表()
    {
        // Arrange
        await CreateTestFile(_user1Id, FileAccessLevel.Private, "private.txt");

        // Act
        var publicFiles = await _libraryService.ListPublicFilesAsync();

        // Assert
        publicFiles.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 创建测试文件
    /// </summary>
    private async Task<UploadedFile> CreateTestFile(
        string ownerId,
        FileAccessLevel accessLevel,
        string fileName)
    {
        var file = UploadedFile.Create(
            sessionId: Guid.NewGuid().ToString(),
            fileName: fileName,
            filePath: Path.Combine(_fixture.TestRootDirectory, fileName),
            fileType: Path.GetExtension(fileName),
            fileSize: 1024,
            ownerId: ownerId,
            summary: $"Test file: {fileName}",
            tags: "test",
            accessLevel: accessLevel);

        return await _fixture.Repository.SaveAsync(file);
    }

    #endregion
}
