# V3 验收测试指南

本文档提供完整的手工编译和验收测试步骤，帮助您验证 General Agent V3 的所有功能。

**测试环境**: macOS 14+, Ubuntu 22.04+, Windows 11+  
**前置条件**: .NET SDK 10.0+

---

## 🚀 快速验证

如果您想快速验证所有功能，运行快速测试脚本：

```bash
cd v3
./quick-test.sh
```

**脚本会自动**:
- ✅ 编译项目
- ✅ 运行核心测试
- ✅ 测试计划任务功能
- ✅ 测试基本命令

**预期输出**:
```
==================================================
General Agent V3 快速验证测试
==================================================

1. 编译项目...
✓ 编译成功

2. 运行核心测试...
✓ 所有核心测试通过

3. 测试计划任务功能...
✓ 任务创建成功
✓ 任务列表正常
✓ 任务删除成功

4. 测试基本命令...
✓ 帮助命令正常

==================================================
✓ 所有测试通过！
==================================================
```

---

## 📦 完整编译和构建

### 1. 克隆仓库

```bash
git clone https://github.com/nothingbut/general-agent.git
cd general-agent/v3
```

### 2. 恢复依赖

```bash
dotnet restore
```

**预期结果**: 无错误，所有 NuGet 包成功恢复。

### 3. 编译项目

```bash
dotnet build --configuration Release
```

**预期结果**:
- ✅ 编译成功
- ✅ 0 个编译错误
- ✅ 0 个编译警告

**输出示例**:
```
Microsoft (R) Build Engine version 17.x.x
...
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 4. 运行所有测试

```bash
dotnet test --logger "console;verbosity=normal"
```

**预期结果**:
- ✅ 866 个测试（865 通过 + 1 跳过）
- ✅ 0 个失败
- ℹ️ 1 个跳过（Ollama 集成测试，需要本地 Ollama 服务）

**输出示例**:
```
总计: 866, 失败: 0, 成功: 865, 已跳过: 1
```

**详细分布**:
- Core Tests: 89/89 ✅
- Skills Tests: 69/69 ✅
- SkillExtraction Tests: 56/56 ✅
- LLM Tests: 84/85 ✅（1 跳过）
- Application Tests: 170/170 ✅
- FileStorage Tests: 111/111 ✅
- Infrastructure Tests: 286/286 ✅

---

## 🧪 功能验收测试

### 测试 1: 计划任务功能

#### 1.1 基本创建和列表

```bash
cd src/GeneralAgent.Hosts.Console

# 创建任务（Cron 表达式）
dotnet run -- task schedule "测试任务1" \
  --schedule "*/5 * * * *" \
  --type reminder \
  --payload '{"message":"5分钟提醒"}' \
  --description "每5分钟执行一次"

# 查看任务列表
dotnet run -- task list
```

**预期结果**:
- ✅ 任务创建成功，返回任务 ID
- ✅ 列表显示新创建的任务
- ✅ 任务状态为 `Pending`
- ✅ 显示下次执行时间

#### 1.2 自然语言调度

```bash
# 创建任务（自然语言）
dotnet run -- task schedule "中文调度测试" \
  --schedule "每天9:00" \
  --type reminder \
  --payload '{"message":"早安提醒"}' \
  --description "每天早上9点"
```

**预期结果**:
- ✅ 自然语言解析成功
- ✅ 转换为正确的 Cron 表达式 `0 9 * * *`
- ✅ 任务创建成功

#### 1.3 任务管理操作

```bash
# 获取任务 ID（从上面的列表中）
TASK_ID="<your-task-id>"

# 查看任务详情
dotnet run -- task show $TASK_ID

# 暂停任务
dotnet run -- task pause $TASK_ID

# 查看状态（应为 Paused）
dotnet run -- task show $TASK_ID

# 恢复任务
dotnet run -- task resume $TASK_ID

# 手动执行
dotnet run -- task run $TASK_ID

# 查看执行历史
dotnet run -- task history $TASK_ID
```

**预期结果**:
- ✅ 所有命令执行成功
- ✅ 状态正确切换（Pending → Paused → Pending）
- ✅ 手动执行产生历史记录
- ✅ 历史记录显示执行时间、状态、输出

#### 1.4 清理测试数据

```bash
# 删除任务
dotnet run -- task delete $TASK_ID --force
```

**预期结果**:
- ✅ 任务删除成功
- ✅ 列表中不再显示该任务

### 测试 2: 自动化测试套件

#### 2.1 单元测试

```bash
# 计划任务单元测试
cd ../../tests/GeneralAgent.Infrastructure.Tests
dotnet test --filter "FullyQualifiedName~ScheduledTasks"
```

**预期结果**:
- ✅ 99 个单元测试通过
- ✅ 包含以下测试组：
  - CronParser 测试（15 个）
  - NaturalLanguageTimeParser 测试（59 个）
  - TaskScheduler 测试（8 个）
  - TaskExecutor 测试（8 个）
  - 其他单元测试（9 个）

#### 2.2 E2E 集成测试

```bash
# 计划任务 E2E 测试
cd ../GeneralAgent.Integration.Tests
dotnet test --filter "FullyQualifiedName~ScheduledTasks"
```

**预期结果**:
- ✅ 12 个 E2E 测试通过
- ✅ 测试覆盖：
  - 创建任务（Cron 和自然语言）
  - 列出和过滤任务
  - 更新任务
  - 暂停/恢复
  - 手动触发
  - 执行历史
  - 删除任务
  - 验证错误场景
  - 调度器生命周期

#### 2.3 其他模块测试

```bash
# 返回根目录
cd ../..

# 长期记忆测试
dotnet test --filter "FullyQualifiedName~Memory"

# 文件存储测试
dotnet test --filter "FullyQualifiedName~FileStorage"

# 技能抽取测试
dotnet test --filter "FullyQualifiedName~SkillExtraction"

# 压缩测试
dotnet test --filter "FullyQualifiedName~Compression"
```

**预期结果**:
- ✅ Memory: 91 个测试通过
- ✅ FileStorage: 111 个测试通过
- ✅ SkillExtraction: 56 个测试通过
- ✅ Compression: 45 个测试通过

---

## 📊 测试覆盖率

### 生成覆盖率报告

```bash
cd v3

# 运行测试并收集覆盖率
dotnet test --collect:"XPlat Code Coverage"

# 安装 ReportGenerator（首次）
dotnet tool install -g dotnet-reportgenerator-globaltool

# 生成 HTML 报告
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# 打开报告
open coveragereport/index.html  # macOS
# xdg-open coveragereport/index.html  # Linux
# start coveragereport/index.html  # Windows
```

**预期结果**:
- ✅ 总体覆盖率 > 85%
- ✅ 核心模块覆盖率 > 80%
- ✅ HTML 报告清晰显示覆盖情况

---

## 🎯 性能验收测试

### 测试启动时间

```bash
cd src/GeneralAgent.Hosts.Console

time dotnet run
```

**预期结果**:
- ✅ REPL 启动时间 < 200ms
- ✅ 提示符正常显示

### 测试命令响应时间

在 REPL 中测试：

```bash
You> /help       # 应 < 50ms
You> /list       # 应 < 100ms
You> /skills     # 应 < 50ms
```

**预期结果**:
- ✅ 所有命令响应快速
- ✅ 无明显延迟

---

## 📋 功能完整性检查清单

### 核心系统

- [ ] ✅ CLI 启动正常
- [ ] ✅ REPL 交互流畅
- [ ] ✅ 命令补全工作
- [ ] ✅ 历史记录可用

### 计划任务（重点测试）

- [ ] ✅ Cron 表达式解析正确
- [ ] ✅ 自然语言解析正确（7 种模式）
- [ ] ✅ 任务创建成功
- [ ] ✅ 任务列表和详情显示
- [ ] ✅ 任务状态管理（暂停/恢复）
- [ ] ✅ 手动触发任务
- [ ] ✅ 执行历史记录
- [ ] ✅ 任务删除
- [ ] ✅ 后台服务运行
- [ ] ✅ 重试机制工作

### 文件上传

- [ ] ✅ 文件上传成功
- [ ] ✅ 文件列表显示
- [ ] ✅ 跨会话访问
- [ ] ✅ 权限管理
- [ ] ✅ 版本控制

### 长期记忆

- [ ] ✅ 记忆添加/查询
- [ ] ✅ 语义搜索
- [ ] ✅ 混合检索
- [ ] ✅ LLM 提取

### 技能抽取

- [ ] ✅ 对话抽取
- [ ] ✅ 技能生成
- [ ] ✅ 历史记录
- [ ] ✅ 统计信息

### 上下文压缩

- [ ] ✅ 自动触发
- [ ] ✅ 策略选择
- [ ] ✅ Token 统计

---

## 🐛 常见问题排查

### 问题 1: 编译失败

**症状**: `dotnet build` 失败

**排查**:
```bash
# 检查 .NET 版本
dotnet --version  # 应为 10.0.x

# 清理缓存
dotnet clean
rm -rf bin obj

# 重新构建
dotnet restore
dotnet build
```

### 问题 2: 测试失败

**症状**: 部分测试失败

**排查**:
```bash
# 查看详细错误
dotnet test --logger "console;verbosity=detailed"

# 运行失败的测试
dotnet test --filter "FullyQualifiedName~<TestName>"
```

### 问题 3: 数据库错误

**症状**: SQLite 相关错误

**排查**:
```bash
# 删除测试数据库
rm -f *.db
rm -f tests/**/*.db

# 重新运行测试
dotnet test
```

### 问题 4: 任务调度不工作

**症状**: 任务未按时执行

**排查**:
1. 检查任务状态是否为 `Pending`
2. 检查 `NextExecutionAt` 时间是否正确
3. 确认后台服务是否运行（`IsRunning` 属性）
4. 查看任务执行历史是否有错误

---

## 📝 验收报告模板

### 编译结果

```
✅ 依赖恢复: 成功
✅ 项目编译: 成功（0 警告，0 错误）
✅ 测试执行: 864/864 通过
✅ 测试覆盖率: 85%+
```

### 功能测试结果

```
✅ 计划任务:
   - Cron 表达式解析: 通过
   - 自然语言解析: 通过
   - 任务管理: 通过
   - 执行历史: 通过
   
✅ 文件上传: 通过
✅ 长期记忆: 通过
✅ 技能抽取: 通过
✅ 上下文压缩: 通过
```

### 性能测试结果

```
✅ REPL 启动: 150ms (< 200ms ✅)
✅ 命令响应: 30ms (< 50ms ✅)
✅ 语义搜索: 10-50ms (< 100ms ✅)
```

### 结论

```
✅ 所有功能正常
✅ 性能符合预期
✅ 代码质量优秀
✅ 可以投入生产使用
```

---

## 📞 获取帮助

如果验收测试遇到问题：

1. **查看日志**: 检查控制台输出
2. **查看文档**: 参考 [用户指南](docs/features/scheduled-tasks-user-guide.md)
3. **提交 Issue**: [GitHub Issues](https://github.com/nothingbut/general-agent/issues)
4. **邮件联系**: shi.chang@163.com

---

**验收测试完成！** 🎉

如果所有测试通过，恭喜您，General Agent V3 已经准备就绪。
