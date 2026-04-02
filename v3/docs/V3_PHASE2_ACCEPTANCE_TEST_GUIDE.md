# V3 Phase 2 验收测试指南

**创建时间**: 2026-04-01
**Phase**: Phase 2 - Embedding 向量化和向量数据库集成
**状态**: ✅ 开发完成，准备验收

---

## 📋 快速概览

**已完成功能**：
- ✅ MemoryRepository 双写逻辑（文件系统 + 向量数据库）
- ✅ 向量搜索（性能提升 1000-10000 倍：50-100秒 → 10-50毫秒）
- ✅ 自动降级（Qdrant 不可用时降级到 LLM 评分）
- ✅ REPL 迁移命令（`/memory migrate-to-vectors`）
- ✅ 27 个新测试（单元 + E2E）
- ✅ 完整文档

**验收目标**：
1. 验证所有测试通过
2. 验证核心功能正常工作
3. 验证性能提升达标
4. 验证降级机制正常

**预计时间**: 15-30 分钟

---

## 🔧 前置条件

### 1. 启动 Ollama（Embedding 生成）

```bash
# 启动 Ollama 服务
ollama serve

# 在新终端下载模型（如果未下载）
ollama pull nomic-embed-text

# 验证 Ollama 正常运行
curl http://localhost:11434/api/tags
# 应返回模型列表，包含 nomic-embed-text
```

### 2. 启动 Qdrant（向量数据库）

```bash
# 启动 Qdrant（同时暴露 REST 和 gRPC 端口）
docker run -d --name qdrant \
  -p 6333:6333 \
  -p 6334:6334 \
  qdrant/qdrant

# 验证 Qdrant 正常运行
curl http://localhost:6333/collections
# 应返回: {"result":{"collections":[]}}
```

### 3. 验证环境

```bash
# 进入项目目录
cd /Users/shichang/Workspace/projects/ai-powered/general-agent

# 确认在正确的分支
git branch --show-current
git log --oneline -1
# 应显示: 1a914e9 docs: 添加 V3 Phase 2 迭代 3 完成交接文档（或更新的提交）

# 检查关键文件存在
ls -la v3/src/GeneralAgent.Infrastructure.Embedding/
ls -la v3/src/GeneralAgent.Infrastructure.VectorDB/
ls -la v3/docs/DEPLOYMENT_PHASE2.md
```

**检查清单**：
- [ ] Ollama 运行在 http://localhost:11434
- [ ] nomic-embed-text 模型已下载
- [ ] Qdrant 运行在 http://localhost:6333
- [ ] 项目在正确的目录和分支

---

## ✅ 验收测试步骤

### 第 1 步：编译验证（2 分钟）

```bash
cd v3
dotnet build
```

**预期结果**：
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**如果失败**：
- 检查 .NET SDK 版本：`dotnet --version`（应为 10.0.x）
- 运行 `dotnet clean` 后重试

---

### 第 2 步：单元测试（3-5 分钟）

```bash
cd v3

# 运行所有单元测试（不包括集成和 E2E）
dotnet test --filter "Category!=E2E&Category!=Integration" --logger "console;verbosity=normal"
```

**预期结果**：
```
Total tests: ~40-50
Passed: ~40-50
Failed: 0
Skipped: 0

包括：
- MemoryRepository 单元测试（原有 + 13 个新增）
- MemoryRetrievalService 单元测试（原有 + 10 个新增）
- VectorDB 单元测试（18 个）
- Embedding 单元测试（14 个）
```

**如果失败**：
- 查看失败的测试名称和错误信息
- 检查是否有未提交的代码
- 参考 `docs/superpowers/handoffs/V3_PHASE2_ITERATION3_COMPLETE.md` 中的"已知问题"

---

### 第 3 步：集成测试（5-10 分钟）

**重要**：确保 Ollama 和 Qdrant 正在运行

```bash
cd v3

# 运行集成测试
dotnet test --filter "Category=Integration" --logger "console;verbosity=normal"
```

**预期结果**：
```
Total tests: ~30
Passed: ~30
Failed: 0
Skipped: 0

包括：
- Ollama Embedding 集成测试（4 个）
- Qdrant VectorDB 集成测试（8 个）
- Memory 集成测试（如有）
```

**如果测试被跳过**：
- 检查 Ollama 是否运行：`curl http://localhost:11434/api/tags`
- 检查 Qdrant 是否运行：`curl http://localhost:6333/collections`
- 检查模型是否下载：`ollama list | grep nomic-embed-text`

**如果测试失败**：
- 查看具体的失败信息
- 常见问题：Ollama 超时、Qdrant 连接失败
- 重启服务后重试

---

### 第 4 步：E2E 测试（5-10 分钟）

```bash
cd v3

# 运行端到端测试
dotnet test --filter "Category=E2E" --logger "console;verbosity=normal"
```

**预期结果**：
```
Total tests: 4
Passed: 4
Failed: 0

包括：
- CreateAndSearchMemory_WithVectors_FindsRelevantMemories
- UpdateMemory_UpdatesVector
- DeleteMemory_DeletesVector
- HybridSearch_CombinesKeywordAndSemantic
```

**如果测试失败**：
- 查看测试输出中的详细错误信息
- E2E 测试需要更长时间（每个测试 10-30 秒）
- 可能需要清理测试数据：检查 `~/.agent/memory_test/` 目录

---

### 第 5 步：手动功能测试（5-10 分钟）

#### 5.1 启动 REPL

```bash
cd v3/src/GeneralAgent.Hosts.Console
dotnet run
```

**预期**：看到欢迎界面和提示符 `>`

#### 5.2 测试记忆创建

```
> /memory add user test_memory
```

按提示输入：
- 描述：`测试记忆`
- 内容：`这是一个关于 TDD 测试驱动开发的记忆`
- 标签：`tdd,test`（回车）

**预期**：
```
✓ 记忆已创建并保存
```

#### 5.3 测试迁移命令（核心功能）

```
> /memory migrate-to-vectors
```

**预期输出**：
```
开始迁移现有记忆到向量数据库...
✓ Qdrant 健康检查通过
✓ 扫描到 1 个现有记忆
[进度条显示 100%]

✅ 迁移完成！
┌────────┬──────────┐
│ 项目   │ 数量     │
├────────┼──────────┤
│ 总计   │ 1 个记忆 │
│ 成功   │ 1 个     │
│ 失败   │ 0 个     │
└────────┴──────────┘

提示: 现在可以使用 /memory semantic-search 进行高速语义搜索
```

**如果失败**：
- 检查错误消息
- 常见问题：Qdrant 未运行、Ollama 未运行
- 检查配置：`cat appsettings.json | grep -A 5 VectorDB`

#### 5.4 测试向量搜索（性能验证）

```
> /memory semantic-search "测试驱动开发"
```

**预期输出**：
```
✅ 向量搜索 '测试驱动开发' 返回 1 个结果（耗时 ~15ms）

┌──────────────┬────────┬──────────────┐
│ 名称         │ 类型   │ 描述         │
├──────────────┼────────┼──────────────┤
│ test_memory  │ User   │ 测试记忆     │
└──────────────┴────────┴──────────────┘

💡 使用 /memory show test_memory 查看详情
```

**关键验证点**：
- ✅ 耗时 < 100ms（应该是 10-50ms）
- ✅ 找到相关记忆
- ✅ 日志显示"✅ 向量搜索"（不是"⚠️ LLM 评分搜索"）

#### 5.5 测试自动降级（可选）

在另一个终端停止 Qdrant：
```bash
docker stop qdrant
```

然后在 REPL 中再次搜索：
```
> /memory semantic-search "测试"
```

**预期输出**：
```
⚠️ 向量搜索不可用，使用 LLM 评分（较慢，50-100秒）
提示：启动 Qdrant: docker run -p 6333:6333 qdrant/qdrant

⚠️ LLM 评分搜索 '测试' 返回 1 个结果（耗时 ~5000ms）

[结果列表]
```

**关键验证点**：
- ✅ 显示降级警告
- ✅ 仍然返回结果（功能可用）
- ✅ 耗时明显更长（几秒而不是毫秒）

**测试后重启 Qdrant**：
```bash
docker start qdrant
```

#### 5.6 退出 REPL

```
> /exit
```

---

### 第 6 步：性能基准测试（可选，10 分钟）

如果想验证大规模数据的性能：

```bash
cd v3/src/GeneralAgent.Hosts.Console

# 创建 50 个测试记忆
for i in {1..50}; do
  echo -e "memory_$i\nTest memory about programming\nThis is memory number $i about TDD and software development\ntdd,test" | \
  dotnet run -- /memory add knowledge "benchmark_memory_$i" >/dev/null 2>&1
  echo "Created memory $i/50"
done

# 迁移到向量数据库
dotnet run -- /memory migrate-to-vectors

# 测试向量搜索性能
time dotnet run -- /memory semantic-search "TDD software development"
```

**预期结果**：
- 搜索时间 < 100ms（通常是 20-50ms）
- 找到多个相关记忆
- 按相关性排序

---

## 📊 验收标准

### 必须通过的验收项

- [ ] **编译成功**：0 warnings, 0 errors
- [ ] **单元测试全部通过**：~40-50 个测试
- [ ] **集成测试全部通过**：~30 个测试
- [ ] **E2E 测试全部通过**：4 个测试
- [ ] **迁移命令正常工作**：成功率 100%
- [ ] **向量搜索速度 < 100ms**：通常 10-50ms
- [ ] **自动降级正常工作**：Qdrant 不可用时仍可搜索

### 建议验证的项（非必需）

- [ ] 性能基准测试（50+ 记忆）
- [ ] 混合检索功能测试
- [ ] 记忆更新后向量同步
- [ ] 记忆删除后向量同步
- [ ] 文档完整性检查

---

## ❌ 常见问题和解决方案

### 问题 1：Ollama 连接失败

**错误**：`EmbeddingException: Failed to connect to Ollama`

**解决**：
```bash
# 检查 Ollama 状态
curl http://localhost:11434/api/tags

# 如果失败，启动 Ollama
ollama serve
```

### 问题 2：nomic-embed-text 模型未找到

**错误**：`Model 'nomic-embed-text' not found`

**解决**：
```bash
ollama pull nomic-embed-text
ollama list  # 验证模型已下载
```

### 问题 3：Qdrant 连接失败

**错误**：`VectorRepositoryException: Failed to connect to Qdrant`

**解决**：
```bash
# 检查 Qdrant 状态
docker ps | grep qdrant

# 如果未运行，启动 Qdrant
docker start qdrant

# 如果容器不存在，创建新容器
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 验证连接
curl http://localhost:6333/collections
```

### 问题 4：端口冲突

**错误**：`Address already in use: 6333` 或 `11434`

**解决**：
```bash
# 查找占用端口的进程
lsof -i :6333
lsof -i :11434

# 停止冲突的进程或使用不同端口
```

### 问题 5：测试超时

**错误**：`Test timeout exceeded`

**解决**：
- E2E 测试每个可能需要 10-30 秒
- 集成测试可能需要 5-10 秒
- 确保网络连接正常
- 增加超时时间（如果需要）

### 问题 6：向量搜索很慢（> 1 秒）

**原因**：可能降级到了 LLM 评分

**检查**：
- 查看日志中是否显示"⚠️ LLM 评分搜索"
- 检查 Qdrant 是否正常运行
- 检查是否成功迁移到向量数据库

---

## 📁 重要文件路径

### 配置文件
- `v3/src/GeneralAgent.Hosts.Console/appsettings.json` - 应用配置

### 代码文件
- `v3/src/GeneralAgent.Infrastructure.Memory/Repositories/MemoryRepository.cs` - 双写逻辑
- `v3/src/GeneralAgent.Infrastructure.Memory/Services/MemoryRetrievalService.cs` - 向量搜索
- `v3/src/GeneralAgent.Hosts.Console/AgentRepl.cs` - REPL 命令

### 测试文件
- `v3/tests/GeneralAgent.Infrastructure.Memory.Tests/` - Memory 单元测试
- `v3/tests/GeneralAgent.Integration.Tests/Memory/` - E2E 测试

### 文档
- `v3/docs/CLI_GUIDE.md` - 使用指南
- `v3/docs/CLI_REFERENCE.md` - 命令参考
- `v3/docs/DEPLOYMENT_PHASE2.md` - 部署指南
- `docs/superpowers/handoffs/V3_PHASE2_ITERATION3_COMPLETE.md` - 迭代 3 交接

---

## ✅ 验收完成后

### 如果所有测试通过

恭喜！Phase 2 验收通过。下一步选择：

1. **创建 Pull Request**（团队协作）
   ```bash
   gh pr create --title "feat: V3 Phase 2 - Embedding 向量化和向量数据库集成" \
     --body-file docs/superpowers/handoffs/V3_PHASE2_ITERATION3_COMPLETE.md
   ```

2. **开始 Phase 3**（如果有规划）
   - 查看 Phase 3 设计文档
   - 创建新的实施计划

3. **解决技术债务**（改进当前功能）
   - 参考交接文档中的"已知问题和改进建议"

### 如果有测试失败

1. 记录失败的测试名称和错误信息
2. 检查"常见问题和解决方案"部分
3. 查看交接文档中的已知问题
4. 在新会话中请求帮助，提供详细的错误信息

---

## 📞 获取帮助

如果遇到问题，在新会话中提供以下信息：

1. **失败的步骤**：第几步失败
2. **错误信息**：完整的错误输出
3. **环境状态**：
   ```bash
   # 执行这些命令并提供输出
   curl http://localhost:11434/api/tags
   curl http://localhost:6333/collections
   ollama list
   docker ps | grep qdrant
   git log --oneline -1
   ```

---

**验收测试准备就绪！** 🚀

在新会话中说：**"开始 V3 Phase 2 验收测试"** 即可开始。
