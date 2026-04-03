# 文档重组计划

**日期**: 2026-04-03
**目标**: 整理 V1、V2、V3 的文档结构，建立清晰的文档体系

---

## 📊 当前状况分析

### 根目录（V1 相关）
```
./
├── README.md                  # 项目总览 ✅ 保留
├── ROADMAP.md                 # 路线图 ✅ 保留
├── CLAUDE.md                  # Claude Code 指南 ✅ 保留
├── CHANGELOG.md               # 变更日志 ✅ 保留
└── docs/                      # V1 技术文档
    ├── api.md                 # API 文档 ✅ 保留
    ├── mcp.md                 # MCP 指南 ✅ 保留
    ├── skills.md              # 技能系统 ✅ 保留
    ├── tui.md                 # TUI 指南 ✅ 保留
    ├── RAG_GUIDE.md           # RAG 指南 ✅ 保留
    ├── OLLAMA_SETUP.md        # Ollama 安装 ✅ 保留
    ├── ACCEPTANCE_TEST.md     # 验收测试 ✅ 保留
    ├── mcp-phase-*.md         # MCP 阶段文档 📦 归档
```

**问题**:
- V1 文档混杂在根目录
- 缺少统一的文档索引
- 部分阶段性文档应该归档

### v2/ 目录
```
v2/
├── README.md                  # V2 总览 ✅ 保留
├── HANDOFF*.md (7 个)         # 交接文档 📦 归档
├── WEEK*.md (9 个)            # 周进度 📦 归档
├── PHASE*.md (3 个)           # 阶段文档 📦 归档
├── DAY*.md (1 个)             # 日进度 📦 归档
├── MIGRATION*.md (2 个)       # 迁移文档 📦 归档
├── DEPLOYMENT_GUIDE.md        # 部署指南 ✅ 保留
├── ACCEPTANCE_TEST.md         # 验收测试 ✅ 保留
└── docs/
    ├── API.md                 # API 文档 ✅ 保留
    ├── ARCHITECTURE.md        # 架构文档 ✅ 保留
    ├── SKILLS.md              # 技能文档 ✅ 保留
    ├── WORKFLOW_INTEGRATION_GUIDE.md ✅ 保留
    ├── v1-*.md (2 个)         # V1 特性 📦 归档到 V1
    ├── plans/ (12 个)         # 计划文档 ✅ 保留
    ├── progress/ (15 个)      # 进度文档 📦 归档
    ├── testing/ (3 个)        # 测试文档 ✅ 保留
    └── workflow/ (2 个)       # Workflow 文档 ✅ 保留
```

**问题**:
- 根目录堆积 22 个历史进度文档
- progress/ 目录文档过多
- 缺少用户指南

### v3/ 目录
```
v3/
├── README.md                  # V3 总览 ✅ 保留
├── HANDOFF*.md (5 个)         # 交接文档 📦 归档
├── V3_PHASE*.md (14 个)       # 阶段文档 📦 归档
├── README_PHASE*.md (2 个)    # 阶段说明 📦 归档
├── MANUAL_ACCEPTANCE_CHECKLIST.md ✅ 移到 docs/
├── QUICK_FIX_GUIDE.md         # 快速修复 ✅ 移到 docs/
└── docs/
    ├── CLI_GUIDE.md           # CLI 指南 ✅ 保留
    ├── CLI_REFERENCE.md       # CLI 参考 ✅ 保留
    ├── SKILLS_GUIDE.md        # 技能指南 ✅ 保留
    ├── FILE_UPLOAD_USER_GUIDE.md # 文件上传 ✅ 保留
    ├── tool-calling.md        # 工具调用 ✅ 保留
    ├── V3_PRIORITY_FEATURES_ROADMAP.md ✅ 保留
    ├── V3_PHASE_FILE_UPLOAD_PLAN.md ✅ 保留
    ├── V3_PHASE2_TECH_DEBT_PLAN.md 📦 归档
    ├── V3_PHASE2_ACCEPTANCE_TEST_GUIDE.md 📦 归档
    ├── V3.1_*.md (4 个)       # V3.1 文档 📦 移到 releases/
    └── DEPLOYMENT_PHASE2.md   # 部署文档 ✅ 保留
```

**问题**:
- 根目录堆积 21 个历史阶段文档
- 缺少统一的架构文档
- V3.1 发布文档应该独立分类

---

## 🎯 整理目标

### 1. 建立统一的文档结构

```
general-agent/
├── README.md                  # 项目总览
├── ROADMAP.md                 # 总体路线图
├── CLAUDE.md                  # Claude Code 工作指南
├── CHANGELOG.md               # 变更日志
│
├── docs/                      # 公共文档（跨版本）
│   ├── README.md              # 文档索引 🆕
│   ├── getting-started/       # 快速开始 🆕
│   │   ├── installation.md
│   │   ├── quickstart.md
│   │   └── ollama-setup.md
│   ├── guides/                # 用户指南 🆕
│   │   ├── cli-guide.md
│   │   ├── skills-guide.md
│   │   ├── mcp-guide.md
│   │   ├── rag-guide.md
│   │   └── tui-guide.md
│   ├── api/                   # API 文档 🆕
│   │   └── api-reference.md
│   └── archives/              # 历史文档归档 🆕
│       ├── v1/
│       │   └── mcp-phase-documents/
│       ├── v2/
│       │   ├── handoffs/
│       │   ├── progress/
│       │   └── migrations/
│       └── v3/
│           ├── handoffs/
│           └── phases/
│
├── v2/                        # Rust V2 版本
│   ├── README.md              # V2 概览
│   ├── docs/
│   │   ├── README.md          # V2 文档索引 🆕
│   │   ├── ARCHITECTURE.md    # 架构文档
│   │   ├── DEPLOYMENT.md      # 部署指南（重命名）
│   │   ├── ACCEPTANCE_TEST.md # 验收测试
│   │   ├── api/               # API 文档
│   │   │   └── API.md
│   │   ├── features/          # 功能文档 🆕
│   │   │   ├── SKILLS.md
│   │   │   └── WORKFLOW_INTEGRATION.md
│   │   ├── plans/             # 设计计划（保留）
│   │   ├── testing/           # 测试文档（保留）
│   │   └── workflow/          # Workflow 文档（保留）
│   └── ... (代码目录)
│
└── v3/                        # C# V3 版本
    ├── README.md              # V3 概览
    ├── docs/
    │   ├── README.md          # V3 文档索引 🆕
    │   ├── ARCHITECTURE.md    # 架构文档 🆕
    │   ├── DEPLOYMENT.md      # 部署指南
    │   ├── MANUAL_ACCEPTANCE_CHECKLIST.md
    │   ├── QUICK_FIX_GUIDE.md
    │   ├── getting-started/   # 快速开始 🆕
    │   │   ├── installation.md 🆕
    │   │   └── quickstart.md 🆕
    │   ├── guides/            # 用户指南
    │   │   ├── CLI_GUIDE.md
    │   │   ├── CLI_REFERENCE.md
    │   │   ├── SKILLS_GUIDE.md
    │   │   └── FILE_UPLOAD_USER_GUIDE.md
    │   ├── features/          # 功能规划 🆕
    │   │   ├── tool-calling.md
    │   │   ├── V3_PRIORITY_FEATURES_ROADMAP.md
    │   │   └── V3_PHASE_FILE_UPLOAD_PLAN.md
    │   └── releases/          # 发布文档 🆕
    │       └── v3.1/
    │           ├── V3.1_FEATURES.md
    │           ├── V3.1_RELEASE_CHECKLIST.md
    │           ├── V3.1_UAT_PLAN.md
    │           └── V3.1_UAT_REPORT.md
    └── ... (代码目录)
```

### 2. 创建文档索引

在各级目录创建 `README.md` 作为文档导航。

### 3. 归档历史文档

- **V1**: MCP 阶段文档 → `docs/archives/v1/`
- **V2**: Handoff、Week、Phase、Migration → `docs/archives/v2/`
- **V3**: Handoff、Phase → `docs/archives/v3/`

### 4. 优化文档命名

- 统一大小写规则（UPPER_CASE 或 kebab-case）
- 删除版本前缀（在目录结构中体现）
- 使用描述性名称

---

## 📋 执行步骤

### Phase 1: 创建新目录结构 ✅

```bash
# 公共文档目录
mkdir -p docs/getting-started
mkdir -p docs/guides
mkdir -p docs/api
mkdir -p docs/archives/v1
mkdir -p docs/archives/v2/{handoffs,progress,migrations}
mkdir -p docs/archives/v3/{handoffs,phases}

# V2 文档目录
mkdir -p v2/docs/api
mkdir -p v2/docs/features

# V3 文档目录
mkdir -p v3/docs/getting-started
mkdir -p v3/docs/guides
mkdir -p v3/docs/features
mkdir -p v3/docs/releases/v3.1
```

### Phase 2: 移动和重命名文档

#### 2.1 公共文档（根目录）

```bash
# 快速开始
mv docs/OLLAMA_SETUP.md docs/getting-started/ollama-setup.md

# 用户指南
mv docs/mcp.md docs/guides/mcp-guide.md
mv docs/skills.md docs/guides/skills-guide.md
mv docs/RAG_GUIDE.md docs/guides/rag-guide.md
mv docs/tui.md docs/guides/tui-guide.md

# API 文档
mv docs/api.md docs/api/api-reference.md

# 归档 V1 历史文档
mv docs/mcp-phase-*.md docs/archives/v1/
mv docs/ACCEPTANCE_TEST.md docs/archives/v1/
```

#### 2.2 V2 文档

```bash
# 移动特性文档
mv v2/docs/WORKFLOW_INTEGRATION_GUIDE.md v2/docs/features/workflow-integration.md
mv v2/docs/API.md v2/docs/api/api-reference.md

# 归档历史文档
mv v2/HANDOFF*.md docs/archives/v2/handoffs/
mv v2/WEEK*.md docs/archives/v2/progress/
mv v2/PHASE*.md docs/archives/v2/progress/
mv v2/DAY*.md docs/archives/v2/progress/
mv v2/MIGRATION*.md docs/archives/v2/migrations/
mv v2/docs/progress/* docs/archives/v2/progress/
mv v2/docs/v1-*.md docs/archives/v1/

# 重命名主要文档
mv v2/DEPLOYMENT_GUIDE.md v2/docs/DEPLOYMENT.md
```

#### 2.3 V3 文档

```bash
# 移动到 docs/
mv v3/MANUAL_ACCEPTANCE_CHECKLIST.md v3/docs/
mv v3/QUICK_FIX_GUIDE.md v3/docs/

# 整理用户指南
# (已经在 v3/docs/guides/ 下，无需移动)

# 整理功能文档
# (已经在 v3/docs/ 下，可以移到 features/)
mkdir -p v3/docs/features
mv v3/docs/tool-calling.md v3/docs/features/
mv v3/docs/V3_PRIORITY_FEATURES_ROADMAP.md v3/docs/features/priority-features.md
mv v3/docs/V3_PHASE_FILE_UPLOAD_PLAN.md v3/docs/features/file-upload-plan.md

# 整理发布文档
mv v3/docs/V3.1_*.md v3/docs/releases/v3.1/

# 归档历史文档
mv v3/HANDOFF*.md docs/archives/v3/handoffs/
mv v3/V3_PHASE*.md docs/archives/v3/phases/
mv v3/README_PHASE*.md docs/archives/v3/phases/
mv v3/docs/V3_PHASE2_*.md docs/archives/v3/phases/
mv v3/docs/DEPLOYMENT_PHASE2.md docs/archives/v3/phases/
```

### Phase 3: 创建文档索引

创建以下索引文件：
- `docs/README.md` - 公共文档索引
- `v2/docs/README.md` - V2 文档索引
- `v3/docs/README.md` - V3 文档索引

### Phase 4: 更新引用

检查并更新所有文档中的内部链接。

### Phase 5: 清理和验证

- 删除空目录
- 验证所有链接有效
- 检查 git 状态

---

## ✅ 验收标准

1. [ ] 所有文档按功能分类
2. [ ] 历史文档已归档
3. [ ] 每个文档目录有 README.md 索引
4. [ ] 文档命名规范统一
5. [ ] 内部链接全部有效
6. [ ] 根目录清爽（< 10 个文件）
7. [ ] v2/ 和 v3/ 根目录清爽（< 5 个文档）

---

## 📝 注意事项

1. **保留 git 历史**: 使用 `git mv` 而不是 `mv`
2. **备份**: 在开始前创建分支备份
3. **增量提交**: 每完成一个 Phase 就提交
4. **验证链接**: 使用工具验证 Markdown 链接有效性
5. **更新 CI**: 如果有文档检查 CI，需要更新路径

---

## 🎯 预期结果

**清理前**:
- 根目录: 13 个文档
- v2/ 根目录: 22 个文档
- v3/ 根目录: 21 个文档
- 总计: **56 个文档**

**清理后**:
- 根目录: 4 个核心文档
- v2/ 根目录: 1 个 README
- v3/ 根目录: 1 个 README
- docs/: 结构化的文档树
- 总计: 仍然 **56 个文档**，但 **组织清晰**

---

**执行人**: Claude + User
**预计时间**: 1-2 小时
**风险**: 低（使用 git，可随时回滚）
