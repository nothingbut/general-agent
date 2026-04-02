# 仓库空间占用分析报告

**分析时间**: 2026-03-24
**总大小**: 3.5GB

---

## 📊 空间占用分布

### 总体分布
```
总计: 3.5GB
├── .git/            2.5GB  (71%)  ⚠️ 主要问题
│   └── objects/pack 1.9GB         Git 历史压缩包
├── v2/              714MB  (20%)
│   └── target/      712MB         Rust 编译产物 (✅ 已排除)
├── v3/              290MB  (8%)
│   ├── tests/bin    213MB         .NET 测试编译产物 (✅ 已排除)
│   └── src/bin       61MB         .NET 源码编译产物 (✅ 已排除)
├── docs/            1.5MB  (<1%)
├── v1/              1.3MB  (<1%)
└── 其他             0.5MB  (<1%)
```

---

## ⚠️ 主要问题：.git 目录占用 2.5GB

### Git 仓库统计
```bash
$ git count-objects -vH

count: 6,054 个松散对象
size: 606 MB (松散对象)
in-pack: 23,849 个打包对象
packs: 5 个包文件
size-pack: 1.94 GB ⚠️ (打包对象)
```

### 问题原因
**.git 历史中曾经提交过大量编译产物**

在 git 历史中发现的大文件（> 20MB）：
- `v2/target/debug/deps/runtime_tests-*`: 20MB+
- `v2/target/debug/deps/libtokio-*.rlib`: 22-26MB
- `v2/target/debug/deps/libreqwest-*.rlib`: 26MB+
- `v2/target/debug/deps/agent_workflow-*`: 26-28MB
- `v2/target/debug/examples/tui_demo`: 31MB
- 各种 `.rlib`、增量编译缓存等

**这些文件虽然现在已经被 .gitignore 排除，但历史提交仍然存储在 .git 中。**

---

## 📈 本地编译产物（已正确排除）

### V2 (Rust)
```
v2/target/              712MB  (✅ 不再提交)
├── debug/              711MB
│   ├── deps/*.rlib     ~200MB
│   ├── examples/       ~100MB
│   └── incremental/    ~400MB
└── flycheck0/          1MB
```

### V3 (C#)
```
v3/                     290MB
├── tests/*/bin/        213MB  (✅ 不再提交)
│   ├── GeneralAgent.Hosts.Console.Tests/bin/    68MB
│   ├── GeneralAgent.Application.Tests/bin/      66MB
│   └── GeneralAgent.Infrastructure.Tests/bin/   63MB
└── src/*/bin/          61MB   (✅ 不再提交)
    └── GeneralAgent.Hosts.Console/bin/           61MB
```

**这些编译产物现在都被 .gitignore 正确排除了。**

---

## 🎯 解决方案

### 选项 1: 清理 Git 历史（推荐）

使用 `git filter-repo` 或 `BFG Repo-Cleaner` 清理历史中的大文件。

#### 方案 A: 使用 git filter-repo（推荐）
```bash
# 1. 备份仓库
cp -r .git .git.backup

# 2. 安装 git-filter-repo
pip install git-filter-repo

# 3. 清理 target/ 目录
git filter-repo --path v2/target --invert-paths

# 4. 清理 bin/ 和 obj/ 目录
git filter-repo --path-glob 'v3/*/bin' --invert-paths
git filter-repo --path-glob 'v3/*/obj' --invert-paths

# 5. 强制推送到远端
git push origin --force --all
git push origin --force --tags
```

#### 方案 B: 使用 BFG（更简单）
```bash
# 1. 下载 BFG
wget https://repo1.maven.org/maven2/com/madgag/bfg/1.14.0/bfg-1.14.0.jar

# 2. 删除大于 10MB 的文件
java -jar bfg-1.14.0.jar --strip-blobs-bigger-than 10M .

# 3. 清理并推送
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push origin --force --all
```

**预期效果**: .git 从 2.5GB 减少到 ~100MB（节省 2.4GB）

---

### 选项 2: 保持现状（不推荐）

如果不清理历史：
- ✅ **优点**: 保留完整历史，可以回溯
- ❌ **缺点**:
  - clone 时间长（需要下载 2.5GB）
  - 占用磁盘空间大
  - 每次 pull 都慢

---

### 选项 3: 重新开始（最激进）

创建新仓库，只保留最新代码：
```bash
# 1. 重命名旧仓库
mv .git .git.old

# 2. 初始化新仓库
git init
git add .
git commit -m "Initial commit - clean start"

# 3. 推送到远端（需要 --force）
git remote add origin <url>
git push origin main --force
```

**预期效果**: .git 从 2.5GB 减少到 ~10MB（节省 2.49GB）

---

## 📊 清理后预期效果

| 项目 | 当前大小 | 清理后 | 节省 |
|------|----------|--------|------|
| .git | 2.5GB | ~100MB | 2.4GB (96%) |
| v2/target | 712MB | 712MB | - (本地) |
| v3/bin+obj | 274MB | 274MB | - (本地) |
| **总计** | **3.5GB** | **~1.1GB** | **2.4GB (69%)** |

**注**:
- v2/target 和 v3/bin+obj 是本地编译产物，可以随时删除重新生成
- 如果也删除本地编译产物：3.5GB → ~100MB（节省 97%）

---

## ⚠️ 清理警告

### 清理 Git 历史的影响
1. **所有开发者需要重新 clone**
   ```bash
   git clone <url> --depth 1  # 浅克隆（推荐）
   # 或
   git clone <url>            # 完整克隆
   ```

2. **历史 commit SHA 会改变**
   - 已有的 PR/Issue 引用可能失效
   - 需要更新所有本地分支

3. **无法回滚到清理前的状态**
   - 确保备份：`cp -r .git .git.backup`

---

## 🎯 推荐操作

### 立即可做（无风险）
```bash
# 1. 清理本地编译产物（可随时重新生成）
rm -rf v2/target         # 节省 712MB
dotnet clean v3          # 节省 274MB

# 2. Git 垃圾回收
git gc --aggressive --prune=now
```

### 需要协调（有风险，推荐）
```bash
# 清理 Git 历史（需要团队协调）
git filter-repo --path v2/target --invert-paths
git push origin --force --all
```

---

## 📝 预防措施（已完成）

✅ **已添加到 .gitignore**:
```gitignore
# Rust
**/target/
v2/Cargo.lock
**/*.rdb

# .NET
**/bin/
**/obj/
*.user
*.suo
```

✅ **未来不会再提交编译产物**

---

## 📈 总结

### 当前状态
- 总大小: **3.5GB**
- 主要问题: **.git 历史中的编译产物**（2.5GB）
- 本地编译产物: 986MB（可删除）

### 推荐方案
1. **清理本地编译产物**: 立即节省 986MB
2. **清理 Git 历史**: 节省 2.4GB（需要团队协调）
3. **总节省**: 3.38GB（97%）

### 操作优先级
1. ⚡ **立即**: 清理本地编译产物（无风险）
2. 🎯 **推荐**: 清理 Git 历史（需要协调）
3. ⚠️ **可选**: 重新开始（最激进）

---

**状态**: 分析完成
**建议**: 先清理本地编译产物，再协调清理 Git 历史
