using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Services;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Services;

/// <summary>
/// FilePermissionService 单元测试
/// </summary>
[Collection("FileStorage Collection")]
public class FilePermissionServiceTests : IDisposable
{
    private readonly FileStorageFixture _fixture;
    private readonly FilePermissionService _permissionService;
    private readonly IFilePermissionRepository _permissionRepository;
    private readonly string _ownerId = "owner-123";
    private readonly string _otherUserId = "user-456";

    public FilePermissionServiceTests(FileStorageFixture fixture)
    {
        _fixture = fixture;

        // 创建 FilePermissionRepository
        var permRepoLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionRepository>();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_fixture.Options);
        _permissionRepository = new FilePermissionRepository(optionsWrapper, permRepoLogger);

        // 创建 FilePermissionService
        var serviceLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionService>();
        _permissionService = new FilePermissionService(
            _permissionRepository,
            _fixture.Repository,
            serviceLogger);
    }

    #region GrantPermissionAsync Tests

    [Fact]
    public async Task GrantPermissionAsync_应该成功授予新权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);

        // Act
        await _permissionService.GrantPermissionAsync(
            file.Id,
            _otherUserId,
            _ownerId,
            PermissionType.Read);

        // Assert
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);
        permissions.Should().HaveCount(1);
        permissions[0].FileId.Should().Be(file.Id);
        permissions[0].UserId.Should().Be(_otherUserId);
        permissions[0].Permission.Should().Be(PermissionType.Read);
        permissions[0].GrantedBy.Should().Be(_ownerId);
    }

    [Fact]
    public async Task GrantPermissionAsync_应该更新现有权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(
            file.Id, _otherUserId, _ownerId, PermissionType.Read);

        // Act - 更新为写权限
        await _permissionService.GrantPermissionAsync(
            file.Id, _otherUserId, _ownerId, PermissionType.Write);

        // Assert
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);
        permissions.Should().HaveCount(1);
        permissions[0].Permission.Should().Be(PermissionType.Write);
    }

    [Fact]
    public async Task GrantPermissionAsync_文件不存在应该抛出异常()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _permissionService.GrantPermissionAsync(
                nonExistentFileId, _otherUserId, _ownerId, PermissionType.Read));
    }

    [Fact]
    public async Task GrantPermissionAsync_非所有者授权应该抛出异常()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);

        // Act & Assert - 其他用户尝试授权
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _permissionService.GrantPermissionAsync(
                file.Id, "another-user", _otherUserId, PermissionType.Read));
    }

    #endregion

    #region RevokePermissionAsync Tests

    [Fact]
    public async Task RevokePermissionAsync_应该成功撤销权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(
            file.Id, _otherUserId, _ownerId, PermissionType.Read);

        // Act
        await _permissionService.RevokePermissionAsync(file.Id, _otherUserId);

        // Assert
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task RevokePermissionAsync_撤销不存在的权限不应抛出异常()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);

        // Act - 不应抛出异常
        await _permissionService.RevokePermissionAsync(file.Id, _otherUserId);

        // Assert - 通过无异常
        Assert.True(true);
    }

    #endregion

    #region ListPermissionsAsync Tests

    [Fact]
    public async Task ListPermissionsAsync_应该返回文件的所有权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(
            file.Id, "user-1", _ownerId, PermissionType.Read);
        await _permissionService.GrantPermissionAsync(
            file.Id, "user-2", _ownerId, PermissionType.Write);

        // Act
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);

        // Assert
        permissions.Should().HaveCount(2);
        permissions.Should().Contain(p => p.UserId == "user-1" && p.Permission == PermissionType.Read);
        permissions.Should().Contain(p => p.UserId == "user-2" && p.Permission == PermissionType.Write);
    }

    [Fact]
    public async Task ListPermissionsAsync_无权限应该返回空列表()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Private);

        // Act
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);

        // Assert
        permissions.Should().BeEmpty();
    }

    #endregion

    #region UpdateAccessLevelAsync Tests

    [Fact]
    public async Task UpdateAccessLevelAsync_应该成功更新访问级别()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Private);

        // Act
        await _permissionService.UpdateAccessLevelAsync(
            file.Id, _ownerId, FileAccessLevel.Public);

        // Assert
        var updated = await _fixture.Repository.GetByIdAsync(file.Id);
        updated.Should().NotBeNull();
        updated!.AccessLevel.Should().Be(FileAccessLevel.Public);
    }

    [Fact]
    public async Task UpdateAccessLevelAsync_改为私有应该删除所有权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(
            file.Id, "user-1", _ownerId, PermissionType.Read);
        await _permissionService.GrantPermissionAsync(
            file.Id, "user-2", _ownerId, PermissionType.Write);

        // Act
        await _permissionService.UpdateAccessLevelAsync(
            file.Id, _ownerId, FileAccessLevel.Private);

        // Assert
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);
        permissions.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAccessLevelAsync_文件不存在应该抛出异常()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _permissionService.UpdateAccessLevelAsync(
                nonExistentFileId, _ownerId, FileAccessLevel.Public));
    }

    [Fact]
    public async Task UpdateAccessLevelAsync_非所有者更新应该抛出异常()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Private);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _permissionService.UpdateAccessLevelAsync(
                file.Id, _otherUserId, FileAccessLevel.Public));
    }

    #endregion

    #region HasAccessAsync Tests

    [Fact]
    public async Task HasAccessAsync_所有者应该有完全访问权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Private);

        // Act & Assert
        var hasReadAccess = await _permissionService.HasAccessAsync(
            file.Id, _ownerId, PermissionType.Read);
        var hasWriteAccess = await _permissionService.HasAccessAsync(
            file.Id, _ownerId, PermissionType.Write);

        hasReadAccess.Should().BeTrue();
        hasWriteAccess.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccessAsync_公开文件所有人应该有读权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Public);

        // Act
        var hasReadAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Read);
        var hasWriteAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Write);

        // Assert
        hasReadAccess.Should().BeTrue();
        hasWriteAccess.Should().BeFalse(); // 公开文件只读
    }

    [Fact]
    public async Task HasAccessAsync_共享文件应该检查权限表_有权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(
            file.Id, _otherUserId, _ownerId, PermissionType.Read);

        // Act
        var hasAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Read);

        // Assert
        hasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task HasAccessAsync_共享文件应该检查权限表_无权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);

        // Act
        var hasAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Read);

        // Assert
        hasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccessAsync_共享文件只有读权限不应该有写权限()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(
            file.Id, _otherUserId, _ownerId, PermissionType.Read);

        // Act
        var hasReadAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Read);
        var hasWriteAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Write);

        // Assert
        hasReadAccess.Should().BeTrue();
        hasWriteAccess.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccessAsync_私有文件非所有者应该无权访问()
    {
        // Arrange
        var file = await CreateTestFile(_ownerId, FileAccessLevel.Private);

        // Act
        var hasAccess = await _permissionService.HasAccessAsync(
            file.Id, _otherUserId, PermissionType.Read);

        // Assert
        hasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task HasAccessAsync_文件不存在应该返回false()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var hasAccess = await _permissionService.HasAccessAsync(
            nonExistentFileId, _otherUserId, PermissionType.Read);

        // Assert
        hasAccess.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 创建测试文件
    /// </summary>
    private async Task<UploadedFile> CreateTestFile(string ownerId, FileAccessLevel accessLevel)
    {
        var file = UploadedFile.Create(
            sessionId: Guid.NewGuid().ToString(),
            fileName: $"test-{Guid.NewGuid()}.txt",
            filePath: Path.Combine(_fixture.TestRootDirectory, $"test-{Guid.NewGuid()}.txt"),
            fileType: ".txt",
            fileSize: 1024,
            ownerId: ownerId,
            summary: "Test file",
            tags: "test",
            accessLevel: accessLevel);

        return await _fixture.Repository.SaveAsync(file);
    }

    #endregion

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
