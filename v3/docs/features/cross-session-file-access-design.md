# 跨会话文件访问功能设计

## 概述

当前文件上传系统将文件绑定到特定会话，用户无法在新会话中访问历史上传的文件。本设计旨在实现跨会话文件访问，提供全局文件库、权限管理和版本控制功能。

## 设计目标

1. **全局文件库**：用户可以在任何会话中访问自己的文件
2. **权限管理**：支持私有、共享、公开三种访问级别
3. **版本控制**：支持文件更新和版本历史追踪
4. **向后兼容**：不破坏现有的会话隔离功能

## 数据模型变更

### 1. UploadedFile 模型扩展

```csharp
public class UploadedFile
{
    // 现有字段
    public Guid Id { get; set; }
    public string SessionId { get; set; }  // 保留用于首次上传的会话追踪
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string FileType { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? Summary { get; set; }
    public string? Tags { get; set; }
    public string? Metadata { get; set; }
    
    // 新增字段
    public string OwnerId { get; set; }              // 文件所有者用户 ID
    public FileAccessLevel AccessLevel { get; set; } // 访问级别
    public int Version { get; set; }                 // 文件版本号
    public Guid? ParentFileId { get; set; }          // 父版本文件 ID（用于版本链）
    public DateTime? UpdatedAt { get; set; }         // 最后更新时间
    public bool IsLatest { get; set; }               // 是否为最新版本
}

public enum FileAccessLevel
{
    Private = 0,   // 私有：仅所有者可访问
    Shared = 1,    // 共享：指定用户可访问（需要权限记录）
    Public = 2     // 公开：所有用户可访问
}
```

### 2. 文件权限表（新增）

用于支持 Shared 级别的细粒度权限控制。

```csharp
public class FilePermission
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }              // 文件 ID
    public string UserId { get; set; }            // 被授权用户 ID
    public PermissionType Permission { get; set; } // 权限类型
    public DateTime GrantedAt { get; set; }       // 授权时间
    public string GrantedBy { get; set; }         // 授权人 ID
}

public enum PermissionType
{
    Read = 0,      // 只读
    Write = 1      // 读写（可更新文件）
}
```

### 3. 数据库架构变更

**uploaded_files 表新增列：**
```sql
ALTER TABLE uploaded_files ADD COLUMN owner_id TEXT NOT NULL DEFAULT 'system';
ALTER TABLE uploaded_files ADD COLUMN access_level INTEGER NOT NULL DEFAULT 0;
ALTER TABLE uploaded_files ADD COLUMN version INTEGER NOT NULL DEFAULT 1;
ALTER TABLE uploaded_files ADD COLUMN parent_file_id TEXT NULL;
ALTER TABLE uploaded_files ADD COLUMN updated_at TEXT NULL;
ALTER TABLE uploaded_files ADD COLUMN is_latest INTEGER NOT NULL DEFAULT 1;

CREATE INDEX idx_owner_id ON uploaded_files(owner_id);
CREATE INDEX idx_access_level ON uploaded_files(access_level);
CREATE INDEX idx_parent_file_id ON uploaded_files(parent_file_id);
CREATE INDEX idx_is_latest ON uploaded_files(is_latest);
```

**file_permissions 表（新建）：**
```sql
CREATE TABLE file_permissions (
    id TEXT PRIMARY KEY,
    file_id TEXT NOT NULL,
    user_id TEXT NOT NULL,
    permission INTEGER NOT NULL,
    granted_at TEXT NOT NULL,
    granted_by TEXT NOT NULL,
    FOREIGN KEY (file_id) REFERENCES uploaded_files(id) ON DELETE CASCADE
);

CREATE INDEX idx_file_id ON file_permissions(file_id);
CREATE INDEX idx_user_id ON file_permissions(user_id);
CREATE UNIQUE INDEX idx_file_user ON file_permissions(file_id, user_id);
```

## 核心功能设计

### 1. 全局文件库

**FileLibraryService（新增）**

```csharp
public interface IFileLibraryService
{
    // 列出用户可访问的所有文件（跨会话）
    Task<List<UploadedFile>> ListAccessibleFilesAsync(
        string userId,
        FileAccessLevel? filterByLevel = null,
        CancellationToken cancellationToken = default);
    
    // 搜索文件（按名称、标签、摘要）
    Task<List<UploadedFile>> SearchFilesAsync(
        string userId,
        string keyword,
        CancellationToken cancellationToken = default);
    
    // 检查用户是否有文件访问权限
    Task<bool> HasAccessAsync(
        Guid fileId,
        string userId,
        PermissionType requiredPermission = PermissionType.Read,
        CancellationToken cancellationToken = default);
    
    // 获取文件（带权限检查）
    Task<UploadedFile?> GetFileAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default);
}
```

**访问权限判断逻辑：**
```
用户可访问文件 if:
  1. 用户是文件所有者 (file.OwnerId == userId)
  OR
  2. 文件是公开的 (file.AccessLevel == Public)
  OR
  3. 文件是共享的且用户在权限列表中 (file.AccessLevel == Shared AND EXISTS permission)
```

### 2. 权限管理

**FilePermissionService（新增）**

```csharp
public interface IFilePermissionService
{
    // 授予权限
    Task GrantPermissionAsync(
        Guid fileId,
        string userId,
        string grantedBy,
        PermissionType permission,
        CancellationToken cancellationToken = default);
    
    // 撤销权限
    Task RevokePermissionAsync(
        Guid fileId,
        string userId,
        CancellationToken cancellationToken = default);
    
    // 列出文件的所有权限
    Task<List<FilePermission>> ListPermissionsAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);
    
    // 更新文件访问级别
    Task UpdateAccessLevelAsync(
        Guid fileId,
        string ownerId,
        FileAccessLevel newLevel,
        CancellationToken cancellationToken = default);
}
```

### 3. 版本控制

**FileVersionService（新增）**

```csharp
public interface IFileVersionService
{
    // 创建新版本（上传同名文件时）
    Task<UploadedFile> CreateNewVersionAsync(
        Guid parentFileId,
        string filePath,
        string userId,
        CancellationToken cancellationToken = default);
    
    // 获取文件的所有版本
    Task<List<UploadedFile>> GetVersionHistoryAsync(
        Guid fileId,
        CancellationToken cancellationToken = default);
    
    // 恢复到特定版本（创建新版本指向旧版本内容）
    Task<UploadedFile> RestoreVersionAsync(
        Guid fileId,
        int version,
        string userId,
        CancellationToken cancellationToken = default);
    
    // 获取最新版本
    Task<UploadedFile?> GetLatestVersionAsync(
        Guid rootFileId,
        CancellationToken cancellationToken = default);
}
```

**版本管理策略：**
- 每次更新文件时，旧版本的 `IsLatest = false`，新版本的 `IsLatest = true`
- 新版本的 `ParentFileId` 指向上一个版本
- 版本号自动递增
- 物理文件路径包含版本号：`files/{ownerId}/{fileId}/v{version}/{filename}`

## 用户场景

### 场景 1：上传私有文件

```csharp
// 用户在会话 A 上传文件
var file = await fileStorageService.UploadAsync(
    sessionId: "session-a",
    filePath: "/path/to/document.pdf",
    userId: "user-123",
    accessLevel: FileAccessLevel.Private);

// 文件默认私有，仅 user-123 可访问
```

### 场景 2：跨会话访问文件

```csharp
// 用户在会话 B 中列出所有可访问的文件
var files = await fileLibraryService.ListAccessibleFilesAsync("user-123");

// 返回包括在会话 A 上传的文件
foreach (var file in files)
{
    Console.WriteLine($"{file.FileName} - 上传于 {file.UploadedAt}");
}
```

### 场景 3：共享文件给其他用户

```csharp
// user-123 将文件共享给 user-456
await filePermissionService.UpdateAccessLevelAsync(
    fileId: file.Id,
    ownerId: "user-123",
    newLevel: FileAccessLevel.Shared);

await filePermissionService.GrantPermissionAsync(
    fileId: file.Id,
    userId: "user-456",
    grantedBy: "user-123",
    permission: PermissionType.Read);

// user-456 现在可以读取该文件
var sharedFile = await fileLibraryService.GetFileAsync(file.Id, "user-456");
```

### 场景 4：更新文件版本

```csharp
// user-123 上传同名文件的新版本
var newVersion = await fileVersionService.CreateNewVersionAsync(
    parentFileId: originalFile.Id,
    filePath: "/path/to/document_v2.pdf",
    userId: "user-123");

// newVersion.Version == 2
// newVersion.ParentFileId == originalFile.Id
// originalFile.IsLatest == false

// 查看版本历史
var history = await fileVersionService.GetVersionHistoryAsync(originalFile.Id);
// 返回：[v1, v2]
```

## 实施计划

### Phase 1: 数据模型和迁移（2-3 天）

1. 创建 FilePermission 模型
2. 扩展 UploadedFile 模型
3. 创建数据库迁移脚本
4. 实现自动迁移逻辑（为现有数据设置默认值）

### Phase 2: 权限管理（2-3 天）

1. 实现 FilePermissionService
2. 实现 FileLibraryService 的访问权限检查
3. 单元测试权限判断逻辑
4. 集成测试权限管理流程

### Phase 3: 版本控制（2-3 天）

1. 实现 FileVersionService
2. 修改 FileStorageService 支持版本化路径
3. 单元测试版本管理逻辑
4. 集成测试版本控制流程

### Phase 4: 全局文件库（2 天）

1. 实现 FileLibraryService
2. 添加文件搜索功能
3. 实现跨会话文件列表查询
4. 集成测试全局文件库功能

### Phase 5: CLI 和文档（1-2 天）

1. 扩展 FileCommand 支持权限和版本操作
2. 添加 file share, file versions 等子命令
3. 更新用户文档
4. 编写使用示例

## 向后兼容性

1. **现有数据迁移**：
   - 所有现有文件自动设置 `OwnerId = "system"`
   - 默认 `AccessLevel = Private`
   - 默认 `Version = 1, IsLatest = true`

2. **API 兼容性**：
   - FileRepository 的现有方法保持不变
   - FileStorageService.UploadAsync 新增可选参数 `FileAccessLevel accessLevel = FileAccessLevel.Private`
   - 会话隔离查询（ListBySessionAsync）仍然可用

3. **行为兼容性**：
   - 如果调用方不传 userId，默认行为与之前相同（会话隔离）
   - 新功能完全可选，不影响现有工作流

## 安全考虑

1. **权限验证**：所有文件访问必须先调用 `HasAccessAsync` 检查权限
2. **所有者验证**：只有文件所有者可以修改访问级别和授予权限
3. **路径安全**：版本化路径仍然使用 Guid 和版本号防止路径遍历
4. **日志审计**：记录所有权限变更和文件访问操作

## 测试策略

### 单元测试
- FilePermissionService: 授予/撤销权限逻辑
- FileVersionService: 版本创建和历史追踪
- FileLibraryService: 权限检查和文件列表过滤

### 集成测试
- 跨会话文件上传和访问
- 权限共享和访问验证
- 版本更新和恢复
- 文件搜索和过滤

### E2E 测试
- 用户 A 上传文件 → 用户 B 无法访问
- 用户 A 共享文件 → 用户 B 可以访问
- 用户 A 更新文件 → 版本历史正确记录
- 用户 A 恢复旧版本 → 创建新版本指向旧内容

## 性能优化

1. **索引优化**：
   - `idx_owner_id`：加速所有者文件查询
   - `idx_access_level`：加速公开文件查询
   - `idx_file_user`：加速权限检查（复合唯一索引）

2. **查询优化**：
   - ListAccessibleFilesAsync 使用 UNION 查询（所有者文件 + 公开文件 + 共享文件）
   - 分页支持（未来扩展）

3. **缓存策略**：
   - 权限检查结果缓存 5 分钟
   - 文件元数据缓存 10 分钟
   - 版本历史缓存 15 分钟

## 未来扩展

1. **文件共享链接**：生成临时访问链接（带过期时间）
2. **文件夹组织**：支持文件夹层级结构
3. **文件标签系统**：增强搜索和分类
4. **文件协作**：多用户同时编辑和版本合并
5. **配额管理**：限制用户存储空间和文件数量
