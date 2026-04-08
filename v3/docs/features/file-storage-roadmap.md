# 文件存储功能路线图

**创建时间**: 2026-04-08  
**状态**: 基础设施完成，等待测试和 CLI 集成

---

## 📋 功能状态总览

### ✅ 已完成（Phase 1-7）

| Phase | 功能 | 状态 | 完成时间 |
|-------|------|------|----------|
| Phase 1-6 | 基础文件上传系统 | ✅ 完成 | 2026-04-03 |
| Phase 7 | 跨会话文件访问基础设施 | ✅ 完成 | 2026-04-08 |

**Phase 1-6 功能清单**：
- ✅ 文件上传和管理（上传、列表、查看、删除）
- ✅ 多种文件类型支持（文本、代码、配置等 20+ 种）
- ✅ 对话中引用文件（`@file:` 语法）
- ✅ 文件内容自动提取到记忆系统
- ✅ 文件类型检测和内容处理
- ✅ 完整的错误处理
- ✅ 31 个单元/集成测试

**Phase 7 功能清单**（刚刚完成）：
- ✅ 数据模型扩展（FileAccessLevel, PermissionType, FilePermission）
- ✅ 数据库架构变更和迁移系统
- ✅ 权限管理系统（FilePermissionService, FilePermissionRepository）
- ✅ 全局文件库（FileLibraryService）
- ✅ 版本控制系统（FileVersionService）
- ✅ 依赖注入配置
- ✅ 构建验证通过

---

## 🚧 待完成功能

### Phase 8: 测试完善 ⭐⭐⭐⭐⭐

**优先级**: 最高  
**预计耗时**: 3-5 天  
**状态**: 📋 待开始

#### 任务清单

**1. 单元测试** (2 天)
- [ ] `FilePermissionServiceTests.cs` - 权限管理逻辑
  - 授予/撤销权限
  - 更新访问级别
  - 权限检查逻辑
  - 所有者验证
  
- [ ] `FileLibraryServiceTests.cs` - 文件库服务
  - ListAccessibleFilesAsync（所有者+公开+共享）
  - SearchFilesAsync（带权限过滤）
  - GetFileAsync（权限检查）
  - 各种访问级别组合
  
- [ ] `FileVersionServiceTests.cs` - 版本控制
  - CreateNewVersionAsync（版本链）
  - GetVersionHistoryAsync（递归查询）
  - RestoreVersionAsync（恢复逻辑）
  - 版本号递增
  
- [ ] `FilePermissionRepositoryTests.cs` - 仓储层
  - CRUD 操作
  - GetByFileAndUserAsync
  - 级联删除

**2. 集成测试** (1-2 天)
- [ ] `CrossSessionFileAccessIntegrationTests.cs`
  - 用户 A 上传文件 → 用户 B 无法访问
  - 用户 A 共享文件 → 用户 B 可以访问
  - 公开文件 → 所有用户可读
  - 权限更新生效
  
- [ ] `FileVersionIntegrationTests.cs`
  - 创建新版本 → 旧版本标记为非最新
  - 版本历史查询
  - 恢复版本 → 创建新版本指向旧内容
  - 版本链完整性
  
- [ ] `DatabaseMigrationIntegrationTests.cs`
  - 迁移脚本执行
  - 现有数据迁移（owner_id='system'）
  - 幂等性验证
  - 回滚测试

**3. E2E 测试** (1 天)
- [ ] `FileLibraryE2ETests.cs`
  - 完整用户工作流
  - 跨会话文件访问
  - 搜索和过滤
  - 版本管理

**目标**: 达到 80%+ 测试覆盖率

---

### Phase 9: CLI 命令集成 ⭐⭐⭐⭐⭐

**优先级**: 最高  
**预计耗时**: 3-4 天  
**状态**: 📋 待开始

#### 任务清单

**1. 全局文件库命令** (1-2 天)
```bash
# 列出全局文件库
/file library list [--level private|shared|public]
/file library search <keyword>
/file library owned       # 列出我拥有的文件
/file library shared      # 列出与我共享的
/file library public      # 列出公开文件
```

实现：
- [ ] `FileLibraryCommand.cs` - 新建命令类
- [ ] 集成 `IFileLibraryService`
- [ ] 输出格式化（表格显示）
- [ ] 分页支持

**2. 权限管理命令** (1-2 天)
```bash
# 共享文件
/file share <file-id> --user <user-id> --permission read|write

# 撤销权限
/file revoke <file-id> --user <user-id>

# 更新访问级别
/file access <file-id> --level private|shared|public

# 查看权限列表
/file permissions <file-id>
```

实现：
- [ ] `FilePermissionCommand.cs` - 新建命令类
- [ ] 集成 `IFilePermissionService`
- [ ] 用户确认提示
- [ ] 错误处理和友好提示

**3. 版本管理命令** (1 天)
```bash
# 查看版本历史
/file versions <file-id>

# 恢复到特定版本
/file restore <file-id> --version <version-number>

# 查看特定版本的内容
/file content <file-id> --version <version-number>
```

实现：
- [ ] `FileVersionCommand.cs` - 新建命令类
- [ ] 集成 `IFileVersionService`
- [ ] 版本对比视图
- [ ] 恢复确认

**4. 更新现有命令**
- [ ] 更新 `FileUploadCommand` 支持 `--access-level` 参数
- [ ] 更新 `FileListCommand` 显示所有者和访问级别
- [ ] 更新 `FileShowCommand` 显示权限和版本信息

**5. 帮助文档**
- [ ] 更新 `/help file` 命令输出
- [ ] 添加使用示例
- [ ] 错误消息优化

---

### Phase 10: 性能优化 ⭐⭐⭐⭐

**优先级**: 高  
**预计耗时**: 2-3 天  
**状态**: 📋 待开始

#### 优化项

**1. 缓存机制** (1-2 天)
- [ ] 实现 `CachedFileLibraryService`
  - 权限检查结果缓存（5 分钟）
  - 文件元数据缓存（10 分钟）
  - 版本历史缓存（15 分钟）
  
- [ ] 缓存失效策略
  - 文件更新时清除相关缓存
  - 权限变更时清除用户缓存
  - LRU 缓存淘汰

- [ ] 性能测试
  - 缓存命中率监控
  - 响应时间对比
  - 内存使用监控

**2. 查询优化** (1 天)
- [ ] `ListAccessibleFilesAsync` 优化
  - UNION 查询改为 JOIN
  - 添加索引利用率分析
  - 查询计划优化
  
- [ ] 批量操作优化
  - 批量权限检查
  - 批量文件加载
  - 减少数据库往返

**3. 分页支持** (半天)
- [ ] 文件列表分页
- [ ] 搜索结果分页
- [ ] 版本历史分页

**目标性能指标**：
- 权限检查：< 10ms（缓存命中）/ < 50ms（缓存未命中）
- 文件列表：< 100ms（100 个文件）
- 搜索查询：< 200ms（1000 个文件）

---

### Phase 11: 用户文档和指南 ⭐⭐⭐⭐

**优先级**: 高  
**预计耗时**: 2 天  
**状态**: 📋 待开始

#### 文档清单

**1. 用户指南** (1 天)
- [ ] `FILE_STORAGE_USER_GUIDE.md` - 完整用户指南
  - 快速入门
  - 文件上传和管理
  - 跨会话访问
  - 权限管理
  - 版本控制
  - 最佳实践
  - 常见问题

**2. API 文档** (半天)
- [ ] `FILE_STORAGE_API.md` - API 参考
  - IFileLibraryService
  - IFilePermissionService
  - IFileVersionService
  - 示例代码

**3. 迁移指南** (半天)
- [ ] `FILE_STORAGE_MIGRATION_GUIDE.md`
  - 从会话隔离到全局文件库
  - 数据库迁移步骤
  - 向后兼容性说明
  - 常见问题

**4. 更新现有文档**
- [ ] 更新 `CLI_GUIDE.md` - 添加新命令
- [ ] 更新 `CLI_REFERENCE.md` - 命令参考
- [ ] 更新 `FILE_UPLOAD_USER_GUIDE.md` - 跨会话部分

---

### Phase 12: 扩展功能 ⭐⭐⭐

**优先级**: 中  
**预计耗时**: 按需（每个 1-2 周）  
**状态**: 💡 设计阶段

#### 扩展功能列表

**1. 文件共享链接** (1-2 周)
- 生成临时访问链接
- 链接过期时间设置
- 访问次数限制
- 密码保护
- 访问日志

**2. 文件夹组织** (2 周)
- 文件夹层级结构
- 移动文件到文件夹
- 文件夹权限继承
- 文件夹搜索
- 批量操作

**3. 文件标签系统增强** (1 周)
- 标签自动建议
- 标签分类
- 按标签过滤
- 标签云视图
- 标签统计

**4. 文件协作功能** (2-3 周)
- 多用户同时编辑
- 变更通知
- 冲突解决
- 版本合并
- 评论和批注

**5. 配额管理** (1 周)
- 用户存储空间限制
- 文件数量限制
- 配额监控和告警
- 自动清理过期文件
- 配额报告

**6. 文件预览和缩略图** (1-2 周)
- 图片预览
- 代码语法高亮
- PDF 预览
- Markdown 渲染
- 缩略图生成

**7. 文件类型扩展** (1 周)
- PDF 支持（文本提取）
- DOCX 支持（Office 文档）
- 图片 OCR（文字识别）
- 压缩包支持（ZIP, TAR）
- 二进制文件元数据

---

## 📊 优先级排序

### 立即执行（本周）

| Phase | 功能 | 优先级 | 预计耗时 | 理由 |
|-------|------|--------|----------|------|
| Phase 8 | 测试完善 | ⭐⭐⭐⭐⭐ | 3-5 天 | 确保质量，防止回归 |
| Phase 9 | CLI 命令集成 | ⭐⭐⭐⭐⭐ | 3-4 天 | 用户可用性，核心价值 |

### 近期执行（下周）

| Phase | 功能 | 优先级 | 预计耗时 | 理由 |
|-------|------|--------|----------|------|
| Phase 10 | 性能优化 | ⭐⭐⭐⭐ | 2-3 天 | 提升用户体验 |
| Phase 11 | 用户文档 | ⭐⭐⭐⭐ | 2 天 | 降低学习成本 |

### 中期执行（按需）

| Phase | 功能 | 优先级 | 预计耗时 | 理由 |
|-------|------|--------|----------|------|
| Phase 12 | 扩展功能 | ⭐⭐⭐ | 1-2 周/功能 | 增强功能，按需实现 |

---

## 🎯 里程碑

### Milestone 1: 基础设施完成 ✅
**完成时间**: 2026-04-08  
**包含**: Phase 1-7

**成果**:
- ✅ 文件上传和管理
- ✅ 跨会话访问基础设施
- ✅ 权限管理系统
- ✅ 版本控制系统

---

### Milestone 2: 功能完整 🎯
**预计完成**: 2026-04-15  
**包含**: Phase 8-9

**目标**:
- ✅ 80%+ 测试覆盖率
- ✅ 完整的 CLI 命令集
- ✅ 用户可完整使用跨会话文件访问

**交付物**:
- 单元测试 + 集成测试 + E2E 测试
- CLI 命令实现
- 基本用户文档

---

### Milestone 3: 生产就绪 🎯
**预计完成**: 2026-04-20  
**包含**: Phase 10-11

**目标**:
- ✅ 性能优化完成
- ✅ 完整文档
- ✅ 可在生产环境使用

**交付物**:
- 缓存机制
- 查询优化
- 完整用户指南
- API 文档
- 迁移指南

---

### Milestone 4: 功能增强 💡
**预计完成**: 按需  
**包含**: Phase 12

**目标**:
- 根据用户反馈实现扩展功能
- 持续迭代和优化

---

## 📈 性能目标

### 当前性能（Phase 7）
- 权限检查：~ 100ms（无缓存）
- 文件列表：~ 200-500ms（100 个文件）
- 搜索查询：~ 500ms-1s（1000 个文件）

### 目标性能（Phase 10 后）
- 权限检查：< 10ms（缓存命中）/ < 50ms（缓存未命中）
- 文件列表：< 100ms（100 个文件）
- 搜索查询：< 200ms（1000 个文件）
- 版本查询：< 50ms（10 个版本）

### 目标质量（Phase 8 后）
- 测试覆盖率：> 80%
- 构建成功率：100%
- 文档完整度：100%（核心功能）

---

## 🔍 技术债务

当前无明显技术债务，但需注意：

1. **迁移系统简化** - 当前 `DatabaseMigrationManager` 在 FileRepository 中初始化，可能需要提取为独立服务
2. **Logger 类型不匹配** - 使用 `NullLogger<DatabaseMigrationManager>` 临时解决，后续可优化
3. **UploadedFile.Create 签名变更** - 调用方需要更新传入 `ownerId` 参数

---

## 📝 下一步行动

### 本周任务（2026-04-08 至 2026-04-12）

**Day 1-2: 单元测试**
1. 创建测试文件
2. 实现 FilePermissionService 测试
3. 实现 FileLibraryService 测试
4. 实现 FileVersionService 测试
5. 运行测试并修复问题

**Day 3: 集成测试**
1. 创建集成测试文件
2. 实现跨会话访问测试
3. 实现版本控制测试
4. 实现数据库迁移测试

**Day 4-5: CLI 命令**
1. 实现 FileLibraryCommand
2. 实现 FilePermissionCommand
3. 实现 FileVersionCommand
4. 更新现有命令
5. 手工测试和调试

### 下周任务（2026-04-15 至 2026-04-19）

**Day 1-2: 性能优化**
1. 实现缓存机制
2. 查询优化
3. 性能测试和调优

**Day 3-4: 文档**
1. 用户指南
2. API 文档
3. 迁移指南
4. 更新现有文档

**Day 5: 验收和发布**
1. 完整验收测试
2. 性能验证
3. 文档审查
4. 发布 Release Notes

---

## 💬 反馈和建议

如果你对功能规划有任何建议或需要调整优先级，请：
1. 在对话中直接提出
2. 或在 GitHub Issues 中讨论

---

**最后更新**: 2026-04-08  
**维护者**: General Agent Team  
**版本**: File Storage Roadmap v1.0
