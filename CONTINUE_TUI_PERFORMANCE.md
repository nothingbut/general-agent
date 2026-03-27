# 继续 TUI 性能监控开发 - 交接提示词

**使用此提示词**: 在新会话中直接粘贴以下内容继续开发

---

## 📍 快速上下文恢复

```
继续 TUI 性能监控集成开发。

【当前状态】
- 分支: feature/tui-performance-monitor
- Day 1 ✅: PerformanceOverlay 组件（提交 00155ae6）
- Day 2 ✅: 数据绑定 + 集成测试（提交 b1f647ad）
- 进度: 60% 完成，准备 Day 3

【已完成功能】
✅ Ctrl+P 打开/关闭面板
✅ 显示所有指标（耗时、吞吐量、P50/P95/P99）
✅ Tab 切换工作流
✅ 4 个 UI 区域渲染（Header/执行/统计/资源）
✅ 9 个测试（4 单元 + 5 集成）全部通过
✅ 演示程序可运行

【待完成】
Day 3（3-4 小时）: 交互功能优化
Day 4（2-3 小时）: 手动验收 + 文档

【关键文件】
- 组件: v2/crates/agent-tui/src/ui/performance_overlay.rs
- 集成: v2/crates/agent-tui/src/app.rs
- 测试: v2/crates/agent-tui/tests/performance_overlay_integration_test.rs
- 演示: v2/crates/agent-tui/examples/performance_overlay_demo.rs

【验证命令】
cd v2
cargo test -p agent-tui performance_overlay  # 9 个测试
cargo run --example performance_overlay_demo  # 交互演示

【文档】
- TUI_PERFORMANCE_DAY2_COMPLETE.md - Day 2 完成报告
- TUI_PERFORMANCE_MONITOR_PLAN.md - 完整计划
- TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md - 验收测试

【建议第一步】
选项 1: 继续 Day 3 开发（交互优化）
选项 2: 直接运行手动验收测试
选项 3: 检查当前代码质量（clippy/fmt）
```

---

## 🚀 Day 3 快速启动

如果继续开发，从以下开始：

```bash
# 1. 确认分支
git status  # 应该在 feature/tui-performance-monitor

# 2. 运行测试确认基线
cd v2 && cargo test -p agent-tui performance_overlay

# 3. 运行演示查看当前功能
cargo run --example performance_overlay_demo

# 4. 开始 Day 3 任务
# 参考 TUI_PERFORMANCE_MONITOR_PLAN.md Day 3 部分
```

---

## 📋 Day 3 任务清单

### 1. 实时数据更新优化

**目标**: 优化数据查询和渲染性能

**文件**: `v2/crates/agent-tui/src/ui/performance_overlay.rs`

**任务**:
- [ ] 添加工作流列表缓存
- [ ] 实现按需更新（仅在数据变化时）
- [ ] 添加更新间隔控制（避免过度刷新）

**代码位置**:
```rust
impl PerformanceOverlay {
    // 第 45-50 行: get_workflow_list()
    // 第 52-65 行: get_current_metrics()
}
```

### 2. 键盘导航完善

**目标**: 改进用户体验

**文件**: `v2/crates/agent-tui/src/app.rs`

**任务**:
- [ ] 添加 Home/End 快捷键（第一个/最后一个工作流）
- [ ] 优化切换动画（可选）
- [ ] 添加快捷键帮助（H 键显示）

**代码位置**:
```rust
// 第 173-230 行: handle_events()
// 性能 overlay 事件处理在第 176-183 行
```

### 3. 错误处理改进

**目标**: 更健壮的错误处理

**任务**:
- [ ] 添加 Mutex 锁超时
- [ ] 改进空数据提示
- [ ] 添加错误日志（使用 log crate）

### 4. 性能测试

**目标**: 验证性能指标

**创建**: `v2/crates/agent-tui/tests/performance_benchmark_test.rs`

**测试内容**:
- [ ] 100 个工作流的渲染时间
- [ ] 内存占用测试
- [ ] 切换响应时间

---

## 📝 手动验收测试（Day 4）

### 核心 4 个场景（15 分钟）

参考 `TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md` 执行：

1. **场景 1**: 面板打开/关闭（2 分钟）
2. **场景 2**: 基本指标显示（5 分钟）
3. **场景 3**: 百分位数显示（3 分钟）
4. **场景 4**: 键盘控制（5 分钟）

### 验收命令

```bash
cd v2
cargo run --example performance_overlay_demo

# 测试步骤：
# 1. 启动后按任意键进入
# 2. 验证面板打开，数据显示
# 3. Tab 切换 3 个工作流
# 4. 检查指标数值合理性
# 5. Esc 关闭，Ctrl+P 重开
# 6. Q 退出
```

---

## 🔧 代码质量检查

### 运行检查

```bash
cd v2

# 1. Clippy 检查
cargo clippy -p agent-tui

# 2. 格式化检查
cargo fmt -p agent-tui --check

# 3. 测试覆盖
cargo test -p agent-tui

# 4. 构建检查
cargo build -p agent-tui --release
```

### 预期结果

- Clippy: 0 错误，最多 2-3 个 warnings（已知的）
- 格式: 无需格式化
- 测试: 9/9 passed
- 构建: 成功，无错误

---

## 📚 相关上下文

### Git 状态

```bash
git log --oneline -3
# b1f647ad Day 2 - data binding and integration tests
# 00155ae6 Day 1 - add PerformanceOverlay component
# 6f0f3acd feat: add subagent orchestration system
```

### 项目结构

```
v2/
├── crates/
│   ├── agent-tui/
│   │   ├── src/
│   │   │   ├── app.rs               ← 集成点
│   │   │   └── ui/
│   │   │       ├── mod.rs
│   │   │       └── performance_overlay.rs  ← 主组件
│   │   ├── examples/
│   │   │   └── performance_overlay_demo.rs
│   │   └── tests/
│   │       └── performance_overlay_integration_test.rs
│   └── agent-workflow/
│       └── src/workflow/
│           └── performance.rs       ← 监控 API
```

---

## ⚡ 常见问题

### Q: 测试失败怎么办？

```bash
# 重新编译
cargo clean -p agent-tui
cargo test -p agent-tui
```

### Q: 演示程序无法启动？

检查是否在 v2 目录：
```bash
pwd  # 应该显示 .../general-agent/v2
cargo run --example performance_overlay_demo
```

### Q: 如何查看详细日志？

```bash
RUST_LOG=debug cargo run --example performance_overlay_demo
```

---

## 🎯 完成标准

### Day 3 完成条件

- [ ] 所有测试通过
- [ ] Clippy 无错误
- [ ] 代码格式正确
- [ ] 性能基准测试通过

### Day 4 完成条件

- [ ] 手动验收 4 个核心场景通过
- [ ] 文档已更新
- [ ] 准备好合并到 main

---

## 🎉 最终合并

Day 3 + Day 4 完成后：

```bash
# 1. 提交所有更改
git add .
git commit -m "feat(tui): Day 3-4 complete - ready for merge"

# 2. 切换到 main 并合并
git checkout main
git merge feature/tui-performance-monitor --no-ff

# 3. 推送
git push origin main

# 4. 清理分支（可选）
git branch -d feature/tui-performance-monitor
git push origin --delete feature/tui-performance-monitor
```

---

**提示词创建日期**: 2026-03-15
**预计使用**: 下次会话开始时
