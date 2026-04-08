using FluentAssertions;
using GeneralAgent.Infrastructure.FileStorage.Models;
using GeneralAgent.Infrastructure.FileStorage.Repositories;
using GeneralAgent.Infrastructure.FileStorage.Services;
using GeneralAgent.Infrastructure.FileStorage.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Infrastructure.FileStorage.Tests.Integration;

/// <summary>
/// 跨会话文件访问集成测试
/// </summary>
public class CrossSessionFileAccessIntegrationTests : IAsyncLifetime
{
    private FileStorageFixture _fixture = null!;
    private FileStorageService _storageService = null!;
    private FileLibraryService _libraryService = null!;
    private FilePermissionService _permissionService = null!;
    private IFilePermissionRepository _permissionRepository = null!;

    private readonly string _alice = "alice";
    private readonly string _bob = "bob";
    private readonly string _charlie = "charlie";

    public async Task InitializeAsync()
    {
        _fixture = new FileStorageFixture();
        _storageService = _fixture.StorageService;

        // 创建权限仓储
        var permRepoLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionRepository>();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(_fixture.Options);
        _permissionRepository = new FilePermissionRepository(optionsWrapper, permRepoLogger);

        // 创建权限服务
        var permServiceLogger = LoggerFactory
            .Create(builder => builder.AddConsole())
            .CreateLogger<FilePermissionService>();
        _permissionService = new FilePermissionService(
            _permissionRepository,
            _fixture.Repository,
            permServiceLogger);

        // 创建文件库服务
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

    #region 场景测试

    [Fact]
    public async Task 场景1_私有文件只有所有者可访问()
    {
        // Alice 在会话 A 上传私有文件
        var sessionA = Guid.NewGuid().ToString();
        var filePath = CreateTempFile("private-doc.txt", "敏感信息");
        var file = await _storageService.UploadFileAsync(filePath, sessionA, _alice, FileAccessLevel.Private);

        // Alice 在会话 B 可以访问
        var sessionB = Guid.NewGuid().ToString();
        var aliceFiles = await _libraryService.ListAccessibleFilesAsync(_alice);
        aliceFiles.Should().Contain(f => f.Id == file.Id);

        // Bob 无法访问
        var bobFiles = await _libraryService.ListAccessibleFilesAsync(_bob);
        bobFiles.Should().NotContain(f => f.Id == file.Id);

        // Bob 尝试获取文件失败
        var fileForBob = await _libraryService.GetFileAsync(file.Id, _bob);
        fileForBob.Should().BeNull();
    }

    [Fact]
    public async Task 场景2_公开文件所有人可读但不可写()
    {
        // Alice 上传公开文件
        var filePath = CreateTempFile("public-doc.txt", "公开文档");
        var file = await _storageService.UploadFileAsync(
            filePath,
            Guid.NewGuid().ToString(),
            _alice,
            FileAccessLevel.Public);

        // Bob 可以读取
        var hasReadAccess = await _permissionService.HasAccessAsync(file.Id, _bob, PermissionType.Read);
        hasReadAccess.Should().BeTrue();

        // Bob 不能写入
        var hasWriteAccess = await _permissionService.HasAccessAsync(file.Id, _bob, PermissionType.Write);
        hasWriteAccess.Should().BeFalse();

        // Bob 可以在文件列表中看到
        var bobFiles = await _libraryService.ListAccessibleFilesAsync(_bob);
        bobFiles.Should().Contain(f => f.Id == file.Id);
    }

    [Fact]
    public async Task 场景3_共享文件权限管理流程()
    {
        // Alice 上传共享文件
        var filePath = CreateTempFile("shared-doc.txt", "共享文档");
        var file = await _storageService.UploadFileAsync(
            filePath,
            Guid.NewGuid().ToString(),
            _alice,
            FileAccessLevel.Shared);

        // 初始状态：Bob 无法访问
        var bobCanAccess = await _permissionService.HasAccessAsync(file.Id, _bob);
        bobCanAccess.Should().BeFalse();

        // Alice 授予 Bob 读权限
        await _permissionService.GrantPermissionAsync(file.Id, _bob, _alice, PermissionType.Read);

        // Bob 现在可以读取
        var bobCanRead = await _permissionService.HasAccessAsync(file.Id, _bob, PermissionType.Read);
        bobCanRead.Should().BeTrue();

        // Bob 仍然不能写入
        var bobCanWrite = await _permissionService.HasAccessAsync(file.Id, _bob, PermissionType.Write);
        bobCanWrite.Should().BeFalse();

        // Alice 升级 Bob 的权限为写权限
        await _permissionService.GrantPermissionAsync(file.Id, _bob, _alice, PermissionType.Write);

        // Bob 现在可以写入
        bobCanWrite = await _permissionService.HasAccessAsync(file.Id, _bob, PermissionType.Write);
        bobCanWrite.Should().BeTrue();

        // Alice 撤销 Bob 的权限
        await _permissionService.RevokePermissionAsync(file.Id, _bob);

        // Bob 再次无法访问
        bobCanAccess = await _permissionService.HasAccessAsync(file.Id, _bob);
        bobCanAccess.Should().BeFalse();
    }

    [Fact]
    public async Task 场景4_访问级别变更自动清理权限()
    {
        // Alice 创建共享文件并授权给 Bob 和 Charlie
        var filePath = CreateTempFile("team-doc.txt", "团队文档");
        var file = await _storageService.UploadFileAsync(
            filePath,
            Guid.NewGuid().ToString(),
            _alice,
            FileAccessLevel.Shared);

        await _permissionService.GrantPermissionAsync(file.Id, _bob, _alice, PermissionType.Read);
        await _permissionService.GrantPermissionAsync(file.Id, _charlie, _alice, PermissionType.Write);

        // 验证权限已生效
        var permissions = await _permissionService.ListPermissionsAsync(file.Id);
        permissions.Should().HaveCount(2);

        // Alice 将文件改为私有
        await _permissionService.UpdateAccessLevelAsync(file.Id, _alice, FileAccessLevel.Private);

        // 所有权限记录应该被自动删除
        permissions = await _permissionService.ListPermissionsAsync(file.Id);
        permissions.Should().BeEmpty();

        // Bob 和 Charlie 都无法访问
        var bobCanAccess = await _permissionService.HasAccessAsync(file.Id, _bob);
        var charlieCanAccess = await _permissionService.HasAccessAsync(file.Id, _charlie);
        bobCanAccess.Should().BeFalse();
        charlieCanAccess.Should().BeFalse();
    }

    [Fact]
    public async Task 场景5_多用户协作的完整工作流()
    {
        // 1. Alice 创建项目文档（私有）
        var docPath = CreateTempFile("project-plan.txt", "项目计划 v1");
        var doc = await _storageService.UploadFileAsync(
            docPath,
            Guid.NewGuid().ToString(),
            _alice,
            FileAccessLevel.Private);

        // 2. Alice 将文档改为共享并邀请 Bob
        await _permissionService.UpdateAccessLevelAsync(doc.Id, _alice, FileAccessLevel.Shared);
        await _permissionService.GrantPermissionAsync(doc.Id, _bob, _alice, PermissionType.Write);

        // 3. Bob 可以访问并查看文件
        var bobFiles = await _libraryService.ListAccessibleFilesAsync(_bob);
        bobFiles.Should().Contain(f => f.Id == doc.Id);

        // 4. Alice 后来邀请 Charlie 只读
        await _permissionService.GrantPermissionAsync(doc.Id, _charlie, _alice, PermissionType.Read);

        // 5. 验证权限设置
        var aliceCanWrite = await _permissionService.HasAccessAsync(doc.Id, _alice, PermissionType.Write);
        var bobCanWrite = await _permissionService.HasAccessAsync(doc.Id, _bob, PermissionType.Write);
        var charlieCanWrite = await _permissionService.HasAccessAsync(doc.Id, _charlie, PermissionType.Write);
        var charlieCanRead = await _permissionService.HasAccessAsync(doc.Id, _charlie, PermissionType.Read);

        aliceCanWrite.Should().BeTrue();   // 所有者可写
        bobCanWrite.Should().BeTrue();     // Bob 有写权限
        charlieCanWrite.Should().BeFalse(); // Charlie 只能读
        charlieCanRead.Should().BeTrue();

        // 6. Alice 将文档改为公开
        await _permissionService.UpdateAccessLevelAsync(doc.Id, _alice, FileAccessLevel.Public);

        // 7. 所有人都可以读取（包括未授权的用户）
        var anyoneCanRead = await _permissionService.HasAccessAsync(doc.Id, "stranger", PermissionType.Read);
        anyoneCanRead.Should().BeTrue();
    }

    [Fact]
    public async Task 场景6_跨会话文件搜索和访问控制()
    {
        // Alice 创建多个文件
        var alicePrivate = await UploadTestFile(_alice, FileAccessLevel.Private, "alice-secret.txt");
        var alicePublic = await UploadTestFile(_alice, FileAccessLevel.Public, "alice-public.txt");

        // Bob 创建多个文件
        var bobPrivate = await UploadTestFile(_bob, FileAccessLevel.Private, "bob-secret.txt");
        var bobShared = await UploadTestFile(_bob, FileAccessLevel.Shared, "bob-shared.txt");

        // Bob 授予 Alice 对共享文件的访问权限
        await _permissionService.GrantPermissionAsync(bobShared.Id, _alice, _bob, PermissionType.Read);

        // Alice 搜索所有文件
        var aliceAccessibleFiles = await _libraryService.ListAccessibleFilesAsync(_alice);

        // Alice 应该能看到：自己的 2 个文件 + Bob 的公开文件(0个) + Bob 共享给她的文件(1个)
        aliceAccessibleFiles.Should().HaveCount(3);
        aliceAccessibleFiles.Should().Contain(f => f.Id == alicePrivate.Id);
        aliceAccessibleFiles.Should().Contain(f => f.Id == alicePublic.Id);
        aliceAccessibleFiles.Should().Contain(f => f.Id == bobShared.Id);
        aliceAccessibleFiles.Should().NotContain(f => f.Id == bobPrivate.Id);

        // Bob 搜索所有文件
        var bobAccessibleFiles = await _libraryService.ListAccessibleFilesAsync(_bob);

        // Bob 应该能看到：自己的 2 个文件 + Alice 的公开文件(1个)
        bobAccessibleFiles.Should().HaveCount(3);
        bobAccessibleFiles.Should().Contain(f => f.Id == bobPrivate.Id);
        bobAccessibleFiles.Should().Contain(f => f.Id == bobShared.Id);
        bobAccessibleFiles.Should().Contain(f => f.Id == alicePublic.Id);
        bobAccessibleFiles.Should().NotContain(f => f.Id == alicePrivate.Id);
    }

    #endregion

    #region 辅助方法

    private string CreateTempFile(string fileName, string content)
    {
        var filePath = Path.Combine(_fixture.TestRootDirectory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private async Task<UploadedFile> UploadTestFile(
        string ownerId,
        FileAccessLevel accessLevel,
        string fileName)
    {
        var filePath = CreateTempFile(fileName, $"Content of {fileName}");
        return await _storageService.UploadFileAsync(
            filePath,
            Guid.NewGuid().ToString(),
            ownerId,
            accessLevel);
    }

    #endregion
}
