using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Repositories;

/// <summary>
/// FilePermissionRepository 单元测试
/// </summary>
[Collection("FileStorage Collection")]
public class FilePermissionRepositoryTests : IDisposable
{
    private readonly FileStorageFixture _fixture;
    private readonly IFilePermissionRepository _repository;
    private readonly string _userId = "user-123";
    private readonly string _grantedBy = "owner-123";

    public FilePermissionRepositoryTests(FileStorageFixture fixture)
    {
        _fixture = fixture;

        // 创建 FilePermissionRepository
        var logger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionRepository>();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_fixture.Options);
        _repository = new FilePermissionRepository(optionsWrapper, logger);
    }

    /// <summary>
    /// 创建测试文件
    /// </summary>
    private async Task<Guid> CreateTestFileAsync(string ownerId = "owner-123")
    {
        var file = UploadedFile.Create(
            sessionId: Guid.NewGuid().ToString(),
            fileName: $"test-{Guid.NewGuid()}.txt",
            filePath: Path.Combine(_fixture.TestRootDirectory, $"test-{Guid.NewGuid()}.txt"),
            fileType: ".txt",
            fileSize: 1024,
            ownerId: ownerId);

        await _fixture.Repository.SaveAsync(file);
        return file.Id;
    }

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_应该成功保存权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var permission = FilePermission.Create(fileId, _userId, _grantedBy, PermissionType.Read);

        // Act
        var saved = await _repository.SaveAsync(permission);

        // Assert
        saved.Should().NotBeNull();
        saved.Id.Should().Be(permission.Id);
        saved.FileId.Should().Be(fileId);
        saved.UserId.Should().Be(_userId);
        saved.Permission.Should().Be(PermissionType.Read);
        saved.GrantedBy.Should().Be(_grantedBy);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_应该返回保存的权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var permission = FilePermission.Create(fileId, _userId, _grantedBy, PermissionType.Read);
        await _repository.SaveAsync(permission);

        // Act
        var retrieved = await _repository.GetByIdAsync(permission.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(permission.Id);
        retrieved.FileId.Should().Be(fileId);
        retrieved.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetByIdAsync_不存在的ID应该返回null()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var retrieved = await _repository.GetByIdAsync(nonExistentId);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region ListByFileIdAsync Tests

    [Fact]
    public async Task ListByFileIdAsync_应该返回文件的所有权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var perm1 = FilePermission.Create(fileId, "user-1", _grantedBy, PermissionType.Read);
        var perm2 = FilePermission.Create(fileId, "user-2", _grantedBy, PermissionType.Write);
        var otherFileId = await CreateTestFileAsync();
        var perm3 = FilePermission.Create(otherFileId, "user-3", _grantedBy, PermissionType.Read);

        await _repository.SaveAsync(perm1);
        await _repository.SaveAsync(perm2);
        await _repository.SaveAsync(perm3);

        // Act
        var permissions = await _repository.ListByFileIdAsync(fileId);

        // Assert
        permissions.Should().HaveCount(2);
        permissions.Should().Contain(p => p.UserId == "user-1");
        permissions.Should().Contain(p => p.UserId == "user-2");
        permissions.Should().NotContain(p => p.UserId == "user-3");
    }

    [Fact]
    public async Task ListByFileIdAsync_无权限应该返回空列表()
    {
        // Arrange
        var emptyFileId = Guid.NewGuid();

        // Act
        var permissions = await _repository.ListByFileIdAsync(emptyFileId);

        // Assert
        permissions.Should().BeEmpty();
    }

    #endregion

    #region ListByUserIdAsync Tests

    [Fact]
    public async Task ListByUserIdAsync_应该返回用户的所有权限()
    {
        // Arrange
        var file1 = await CreateTestFileAsync();
        var file2 = await CreateTestFileAsync();

        var perm1 = FilePermission.Create(file1, _userId, _grantedBy, PermissionType.Read);
        var perm2 = FilePermission.Create(file2, _userId, _grantedBy, PermissionType.Write);
        var perm3 = FilePermission.Create(file1, "other-user", _grantedBy, PermissionType.Read);

        await _repository.SaveAsync(perm1);
        await _repository.SaveAsync(perm2);
        await _repository.SaveAsync(perm3);

        // Act
        var permissions = await _repository.ListByUserIdAsync(_userId);

        // Assert
        permissions.Should().HaveCount(2);
        permissions.Should().Contain(p => p.FileId == file1);
        permissions.Should().Contain(p => p.FileId == file2);
        permissions.Should().NotContain(p => p.UserId == "other-user");
    }

    [Fact]
    public async Task ListByUserIdAsync_无权限应该返回空列表()
    {
        // Act
        var permissions = await _repository.ListByUserIdAsync("user-without-permissions");

        // Assert
        permissions.Should().BeEmpty();
    }

    #endregion

    #region GetByFileAndUserAsync Tests

    [Fact]
    public async Task GetByFileAndUserAsync_应该返回匹配的权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var permission = FilePermission.Create(fileId, _userId, _grantedBy, PermissionType.Read);
        await _repository.SaveAsync(permission);

        // Act
        var retrieved = await _repository.GetByFileAndUserAsync(fileId, _userId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.FileId.Should().Be(fileId);
        retrieved.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetByFileAndUserAsync_无匹配应该返回null()
    {
        // Act
        var fileId = await CreateTestFileAsync();
        var retrieved = await _repository.GetByFileAndUserAsync(fileId, "non-existent-user");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_应该成功更新权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var permission = FilePermission.Create(fileId, _userId, _grantedBy, PermissionType.Read);
        await _repository.SaveAsync(permission);

        // Act - 更新为写权限
        var updated = permission.WithPermission(PermissionType.Write);
        var result = await _repository.UpdateAsync(updated);

        // Assert
        result.Permission.Should().Be(PermissionType.Write);

        // 验证数据库中的值已更新
        var retrieved = await _repository.GetByIdAsync(permission.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Permission.Should().Be(PermissionType.Write);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_应该成功删除权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var permission = FilePermission.Create(fileId, _userId, _grantedBy, PermissionType.Read);
        await _repository.SaveAsync(permission);

        // Act
        var deleted = await _repository.DeleteAsync(permission.Id);

        // Assert
        deleted.Should().BeTrue();

        // 验证已删除
        var retrieved = await _repository.GetByIdAsync(permission.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_删除不存在的权限应该返回false()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var deleted = await _repository.DeleteAsync(nonExistentId);

        // Assert
        deleted.Should().BeFalse();
    }

    #endregion

    #region DeleteByFileIdAsync Tests

    [Fact]
    public async Task DeleteByFileIdAsync_应该删除文件的所有权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var perm1 = FilePermission.Create(fileId, "user-1", _grantedBy, PermissionType.Read);
        var perm2 = FilePermission.Create(fileId, "user-2", _grantedBy, PermissionType.Write);
        var otherFileId = await CreateTestFileAsync();
        var perm3 = FilePermission.Create(otherFileId, "user-3", _grantedBy, PermissionType.Read);

        await _repository.SaveAsync(perm1);
        await _repository.SaveAsync(perm2);
        await _repository.SaveAsync(perm3);

        // Act
        var deletedCount = await _repository.DeleteByFileIdAsync(fileId);

        // Assert
        deletedCount.Should().Be(2);

        // 验证已删除
        var permissions = await _repository.ListByFileIdAsync(fileId);
        permissions.Should().BeEmpty();

        // 验证其他文件的权限未受影响
        var otherPermission = await _repository.GetByIdAsync(perm3.Id);
        otherPermission.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteByFileIdAsync_无权限应该返回0()
    {
        // Arrange
        var emptyFileId = Guid.NewGuid();

        // Act
        var deletedCount = await _repository.DeleteByFileIdAsync(emptyFileId);

        // Assert
        deletedCount.Should().Be(0);
    }

    #endregion

    #region DeleteByFileAndUserAsync Tests

    [Fact]
    public async Task DeleteByFileAndUserAsync_应该删除特定用户的权限()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();
        var perm1 = FilePermission.Create(fileId, _userId, _grantedBy, PermissionType.Read);
        var perm2 = FilePermission.Create(fileId, "user-2", _grantedBy, PermissionType.Write);

        await _repository.SaveAsync(perm1);
        await _repository.SaveAsync(perm2);

        // Act
        var deleted = await _repository.DeleteByFileAndUserAsync(fileId, _userId);

        // Assert
        deleted.Should().BeTrue();

        // 验证目标权限已删除
        var retrieved = await _repository.GetByFileAndUserAsync(fileId, _userId);
        retrieved.Should().BeNull();

        // 验证其他用户权限未受影响
        var otherPermission = await _repository.GetByFileAndUserAsync(fileId, "user-2");
        otherPermission.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteByFileAndUserAsync_无匹配应该返回false()
    {
        // Arrange
        var fileId = await CreateTestFileAsync();

        // Act
        var deleted = await _repository.DeleteByFileAndUserAsync(fileId, "non-existent-user");

        // Assert
        deleted.Should().BeFalse();
    }

    #endregion

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
