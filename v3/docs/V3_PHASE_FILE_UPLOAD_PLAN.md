# V3 文件上传功能实施计划

**创建时间**: 2026-04-03
**最后更新**: 2026-04-03（增加对话引用功能）
**预计耗时**: 5-7.5 小时（MVP 版）
**优先级**: 高 ⭐⭐⭐⭐
**状态**: 📋 计划中

---

## 🎯 功能目标

实现文件上传功能，使用户能够在对话中上传文档、代码文件，Agent 可以分析和处理文件内容，并自动关联到记忆系统。

---

## 📋 需求分析

### 核心需求

1. **文件上传**
   - 用户通过 REPL 命令上传文件
   - 支持常见文件类型（文本、代码、JSON、YAML 等）
   - 文件存储在本地文件系统

2. **文件管理**
   - 文件元数据管理（名称、类型、大小、上传时间）
   - 文件与会话关联
   - 文件列表和查询

3. **文件处理**
   - 读取文本文件内容
   - 代码文件语法识别
   - 自动摘要和记忆提取

4. **对话集成** ⭐ (MVP 核心功能)
   - 在对话中引用文件（`@file:filename` 或 `@file:<id>`）
   - 自动解析文件引用并加载内容
   - 将文件内容作为上下文传递给 LLM
   - 支持多文件同时引用

### 非功能需求

- **性能**: 文件读取 < 100ms
- **安全**: 文件类型白名单、大小限制
- **可扩展**: 易于添加新的文件处理器
- **测试**: 80%+ 代码覆盖率

---

## 🏗️ 技术方案

### MVP 方案（5-7.5 小时）

**核心原则**: 最小可行产品（MVP），包含完整的上传、管理、对话引用功能

#### 1. 文件存储

```
~/.general-agent/
├── sessions/
│   └── <session-id>/
│       └── files/
│           ├── document.txt
│           ├── code.cs
│           └── config.json
└── files.db  # SQLite 元数据
```

**文件元数据表**:
```sql
CREATE TABLE uploaded_files (
    id TEXT PRIMARY KEY,           -- GUID
    session_id TEXT NOT NULL,       -- 所属会话
    file_name TEXT NOT NULL,        -- 原始文件名
    file_path TEXT NOT NULL,        -- 存储路径（相对路径）
    file_type TEXT NOT NULL,        -- 文件类型（.txt, .cs, .json）
    file_size INTEGER NOT NULL,     -- 文件大小（字节）
    mime_type TEXT,                 -- MIME 类型
    uploaded_at TEXT NOT NULL,      -- 上传时间（ISO 8601）
    summary TEXT,                   -- 文件摘要（可选）
    tags TEXT,                      -- 标签（逗号分隔）
    metadata TEXT                   -- 额外元数据（JSON）
);
```

#### 2. 核心组件

```
GeneralAgent.Infrastructure.FileStorage/
├── Models/
│   ├── UploadedFile.cs              # 文件模型
│   └── FileReference.cs             # 文件引用模型
├── Repositories/
│   └── FileRepository.cs            # 文件仓储
├── Services/
│   ├── FileStorageService.cs        # 文件存储服务
│   ├── FileProcessorService.cs      # 文件处理服务
│   └── FileReferenceParser.cs       # 文件引用解析器 ⭐
├── Processors/
│   ├── IFileProcessor.cs            # 处理器接口
│   ├── TextFileProcessor.cs         # 文本文件
│   ├── CodeFileProcessor.cs         # 代码文件
│   └── JsonFileProcessor.cs         # JSON/YAML
└── Extensions/
    └── MessageExtensions.cs         # 消息扩展（文件引用）
```

**FileReferenceParser 设计**:
```csharp
public class FileReferenceParser
{
    // 解析消息中的文件引用
    public List<FileReference> ParseReferences(string message);
    
    // 解析单个引用字符串（@file:xxx）
    public FileReference? ParseSingleReference(string reference);
    
    // 替换消息中的文件引用为实际内容
    public string ReplaceReferencesWithContent(
        string message, 
        List<UploadedFile> files);
}

public class FileReference
{
    public string OriginalText { get; set; }  // @file:config.json
    public string? FileName { get; set; }     // config.json
    public Guid? FileId { get; set; }         // 或者 GUID
    public int StartIndex { get; set; }       // 在消息中的位置
    public int Length { get; set; }           // 引用长度
}
```

#### 3. REPL 命令

```bash
# 上传文件
/file upload <path>              # 上传本地文件
/file upload <path> --summary    # 上传并生成摘要
/file upload <path> --to-memory  # 上传并转为记忆

# 管理文件
/file list                       # 列出当前会话的文件
/file show <file-id>            # 查看文件详情
/file content <file-id>         # 查看文件内容
/file delete <file-id>          # 删除文件

# 在对话中引用 ⭐ MVP 功能
@file:document.txt              # 按文件名引用
@file:<file-id>                 # 按 ID 引用
@file:config.json @file:code.cs # 引用多个文件
```

#### 4. 文件处理流程

**上传流程**:
```
用户上传文件
    ↓
验证文件（类型、大小）
    ↓
存储到文件系统
    ↓
保存元数据到 SQLite
    ↓
[可选] 生成摘要
    ↓
[可选] 转为记忆
    ↓
返回文件 ID
```

**对话引用流程** ⭐:
```
用户发送消息（包含 @file:xxx）
    ↓
FileReferenceParser 解析引用
    ↓
根据文件名/ID 查询文件
    ↓
加载文件内容
    ↓
格式化文件内容（添加边界标记）
    ↓
替换消息中的引用为实际内容
    ↓
传递给 LLM
    ↓
LLM 基于文件内容生成回复
```

**文件内容格式化示例**:
```markdown
[用户消息]
帮我分析一下 @file:config.json 的配置

[处理后传递给 LLM]
帮我分析一下以下配置文件的内容：

```file: config.json
{
  "database": {
    "host": "localhost",
    "port": 5432
  }
}
```
```

---

## 📊 实施步骤

### Phase 1: 基础架构（2 小时）

**任务**:
1. ✅ 创建 `GeneralAgent.Infrastructure.FileStorage` 项目
2. ✅ 定义 `UploadedFile` 模型
3. ✅ 实现 `FileRepository`（SQLite CRUD）
4. ✅ 实现 `FileStorageService`（文件 I/O）
5. ✅ 配置依赖注入

**验收标准**:
- [ ] 可以保存和读取文件元数据
- [ ] 可以存储和检索文件内容
- [ ] 单元测试覆盖率 > 80%

---

### Phase 2: 文件处理器（1.5 小时）

**任务**:
1. ✅ 定义 `IFileProcessor` 接口
2. ✅ 实现 `TextFileProcessor`（.txt, .md）
3. ✅ 实现 `CodeFileProcessor`（.cs, .py, .js, .rs）
4. ✅ 实现 `JsonFileProcessor`（.json, .yaml）
5. ✅ 文件类型自动检测

**验收标准**:
- [ ] 可以读取文本文件内容
- [ ] 可以识别代码文件类型
- [ ] 可以解析 JSON/YAML
- [ ] 单元测试覆盖 3 个处理器

---

### Phase 3: REPL 集成（1 小时）

**任务**:
1. ✅ 实现 `/file upload` 命令
2. ✅ 实现 `/file list` 命令
3. ✅ 实现 `/file show` 命令
4. ✅ 实现 `/file content` 命令
5. ✅ 实现 `/file delete` 命令

**验收标准**:
- [ ] 所有命令正常工作
- [ ] 错误处理完善
- [ ] 友好的输出格式

---

### Phase 4: 记忆集成（1 小时）

**任务**:
1. ✅ 实现 `--to-memory` 选项
2. ✅ 文件内容自动摘要
3. ✅ 自动创建 Knowledge 类型记忆
4. ✅ 文件和记忆双向关联

**验收标准**:
- [ ] 上传文件可自动创建记忆
- [ ] 记忆包含文件摘要和链接
- [ ] 可以从记忆追溯到文件

---

### Phase 5: 对话引用功能（1-1.5 小时）⭐

**任务**:
1. ✅ 实现文件引用解析器 `FileReferenceParser`
   - 识别 `@file:filename` 模式
   - 识别 `@file:<id>` 模式
   - 支持一条消息中多个文件引用
2. ✅ 集成到消息处理流程
   - 在 `MessageRouter` 中添加文件引用处理
   - 解析引用并加载文件内容
   - 将文件内容附加到消息上下文
3. ✅ 文件内容格式化
   - 添加文件边界标记（```file: filename```）
   - 自动检测代码文件并添加语法高亮标记
   - 限制文件内容长度（避免超出 token 限制）
4. ✅ 错误处理
   - 文件不存在时友好提示
   - 文件名歧义时提示用户使用 ID
   - 文件过大时截断并警告

**验收标准**:
- [ ] 可以用 `@file:filename` 引用文件
- [ ] 可以用 `@file:<id>` 引用文件
- [ ] 可以同时引用多个文件
- [ ] 文件内容正确传递给 LLM
- [ ] 错误处理完善且用户友好

**示例对话**:
```
用户: 帮我分析一下 @file:config.json 的配置
Agent: [读取 config.json 内容并分析]

用户: 根据 @file:requirements.txt 和 @file:setup.py 检查依赖一致性
Agent: [同时读取两个文件并对比]
```

---

### Phase 6: 测试和文档（0.5 小时）

**任务**:
1. ✅ 编写集成测试
2. ✅ 更新 CLI 使用指南
3. ✅ 更新 CLI 命令参考
4. ✅ 编写验收测试指南

**验收标准**:
- [ ] 单元测试覆盖率 > 80%
- [ ] 至少 2 个 E2E 测试
- [ ] 文档完整清晰

---

## 🔧 技术选型

### 文件存储

- **方案**: 本地文件系统 + SQLite 元数据
- **理由**: 简单、无外部依赖、易于备份
- **API**: `System.IO.File`、`System.IO.Path`

### 数据库

- **方案**: SQLite（复用现有基础设施）
- **ORM**: Entity Framework Core（可选）或原生 SQL
- **位置**: `~/.general-agent/files.db`

### 文件类型检测

- **方案**: 文件扩展名 + MIME 类型
- **库**: 无需外部库，使用 `MimeMapping` 或简单映射

### 文本摘要

- **方案**: 调用现有 LLM 服务
- **API**: 复用 `ILLMClient`

---

## 🚫 不在此 Phase 的功能

以下功能延后到后续迭代：

1. ❌ PDF 文档解析（需要 PdfPig 库）
2. ❌ DOCX 文档解析（需要 DocumentFormat.OpenXml）
3. ❌ 图片 OCR（需要 Tesseract.NET）
4. ❌ 文件版本管理
5. ❌ 文件预览和下载
6. ❌ 多文件批量上传
7. ❌ 文件分享和权限
8. ❌ 文件内容搜索和索引

---

## 📏 成功标准

### 功能验收

- [ ] 可以上传文本文件和代码文件
- [ ] 可以列出、查看、删除文件
- [ ] 可以查看文件内容
- [ ] 可以将文件自动转为记忆
- [ ] 文件与会话正确关联
- [ ] **可以在对话中用 `@file:` 引用文件** ⭐
- [ ] **可以同时引用多个文件** ⭐
- [ ] **文件内容正确传递给 LLM** ⭐
- [ ] **文件引用错误处理友好** ⭐

### 质量标准

- [ ] 单元测试覆盖率 > 80%
- [ ] 至少 2 个 E2E 测试
- [ ] 所有现有测试通过
- [ ] 无内存泄漏
- [ ] 错误处理完善

### 文档标准

- [ ] CLI 使用指南包含文件命令
- [ ] CLI 命令参考完整
- [ ] 代码注释完整（XML 注释）
- [ ] 验收测试指南

---

## 🧪 测试计划

### 单元测试

```csharp
// FileRepositoryTests.cs
- SaveFileMetadata_ShouldPersist
- GetFileById_ShouldReturn
- ListFilesBySession_ShouldFilter
- DeleteFile_ShouldRemove

// FileStorageServiceTests.cs
- StoreFile_ShouldSaveToFileSystem
- RetrieveFile_ShouldReadContent
- ValidateFile_ShouldCheckTypeAndSize

// TextFileProcessorTests.cs
- ProcessTextFile_ShouldExtractContent
- ProcessMarkdownFile_ShouldPreserveFormat
```

### 集成测试

```csharp
// FileUploadE2ETests.cs
- UploadTextFile_ShouldWork
- UploadCodeFile_ShouldDetectLanguage
- UploadAndCreateMemory_ShouldLink
- UploadInvalidFile_ShouldReject
- ReferenceFileInConversation_ShouldWork
- ReferenceMultipleFiles_ShouldWork
- ReferenceNonExistentFile_ShouldShowError
```

---

## 🎯 里程碑

### Milestone 1: MVP 完成（5-7.5 小时）
- ✅ 基础架构（2h）
- ✅ 文本和代码文件处理（1.5h）
- ✅ REPL 命令（1h）
- ✅ 记忆集成（1h）
- ✅ **对话引用功能（1-1.5h）** ⭐ 关键功能
- ✅ 测试和文档（0.5h）

### Milestone 2: 增强功能（未来）
- PDF 和 DOCX 支持
- 图片 OCR
- 文件预览
- 批量上传
- 文件版本管理

---

## 🧑‍💻 人工验收测试场景

### 场景 1: 基本上传和查看

```bash
# 1. 上传一个文本文件
> /file upload test.txt
✓ 文件已上传: test.txt (ID: abc123, 大小: 1.2 KB)

# 2. 列出文件
> /file list
Session 文件列表：
  1. test.txt (abc123) - 1.2 KB - 2026-04-03 18:00

# 3. 查看文件内容
> /file content abc123
[显示文件内容]
```

### 场景 2: 对话中引用文件 ⭐

```bash
# 1. 上传配置文件
> /file upload config.json
✓ 文件已上传: config.json (ID: def456)

# 2. 在对话中引用
> 帮我分析一下 @file:config.json 的配置
[Agent 读取 config.json 内容并分析]

# 3. 按 ID 引用（避免文件名歧义）
> 根据 @file:def456 中的配置，生成连接字符串
[Agent 基于文件内容生成]
```

### 场景 3: 多文件引用 ⭐

```bash
# 1. 上传多个文件
> /file upload requirements.txt
✓ 文件已上传: requirements.txt (ID: ghi789)

> /file upload setup.py
✓ 文件已上传: setup.py (ID: jkl012)

# 2. 同时引用多个文件
> 检查 @file:requirements.txt 和 @file:setup.py 中的依赖版本是否一致
[Agent 同时读取两个文件并对比]
```

### 场景 4: 上传并转为记忆

```bash
# 1. 上传并自动创建记忆
> /file upload api-docs.md --to-memory
✓ 文件已上传: api-docs.md (ID: mno345)
✓ 已创建记忆: api-docs (类型: Knowledge)

# 2. 搜索记忆
> /memory search "API"
找到 1 个记忆：
  - api-docs (Knowledge) - 来自文件: api-docs.md
```

### 场景 5: 错误处理

```bash
# 1. 引用不存在的文件
> 请分析 @file:notexist.txt
❌ 错误: 文件 'notexist.txt' 不存在

# 2. 文件名歧义
> /file upload test.txt
> /file upload test.txt
> 请查看 @file:test.txt
⚠️ 警告: 找到多个名为 'test.txt' 的文件，请使用 ID 引用：
  - @file:abc123
  - @file:xyz789

# 3. 上传超大文件
> /file upload huge-file.log
❌ 错误: 文件过大（10.5 MB），最大允许 5 MB
```

### 验收检查清单

- [ ] 所有场景都能正常执行
- [ ] 文件引用在对话中工作正常
- [ ] 多文件引用同时生效
- [ ] 错误提示清晰友好
- [ ] 文件内容正确传递给 LLM
- [ ] LLM 能理解并基于文件内容回答

---

## 📚 参考资料

- [.NET File I/O](https://learn.microsoft.com/en-us/dotnet/standard/io/)
- [SQLite with .NET](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [MIME Types](https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types)
- [记忆系统架构](./CLAUDE.md#记忆系统)

---

## 🔄 下一步

1. **确认方案**: 与用户确认此实施计划
2. **创建项目**: 创建 `GeneralAgent.Infrastructure.FileStorage` 项目
3. **开始开发**: 按照 Phase 1-5 顺序实施
4. **持续测试**: 每个 Phase 完成后运行测试
5. **文档更新**: 实施过程中更新文档

---

**创建者**: Claude Sonnet 4.5
**审核者**: 待审核
**批准者**: 待批准
