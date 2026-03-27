# 下一步行动指南

**更新日期**: 2026-03-14
**当前阶段**: TUI 性能监控集成准备

---

## ✅ 已完成工作

### 1. Subagent 系统合并 ✅
- **分支**: feature/subagent-integration → main
- **合并提交**: 6f0f3acd
- **状态**: 已推送到 origin/main

**包含功能**:
- 并行任务执行（`/subagent start`）
- 实时监控界面（Ctrl+S）
- 数据库持久化
- 完整测试和文档

### 2. 准备文档创建 ✅
已创建以下文档指导接下来的工作：

1. **TUI_PERFORMANCE_MONITOR_PLAN.md**
   - 完整的实施计划（Day 1-4）
   - 架构设计和 API 说明
   - 代码估算（~730 行，10 小时）

2. **TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md**
   - 10 个手动验收测试场景
   - 快速验收检查清单
   - 问题记录模板

3. **MERGE_COMPLETE.md**
   - Subagent 合并完成报告
   - 项目现状总结

---

## 🎯 当前任务：TUI 性能监控集成

### 优先级
**P1** - 短期计划中的第二项任务

### 预计时间
3-4 天（~10 小时实际编码）

### 核心目标
在 TUI 中添加性能监控面板，显示工作流执行指标。

### 必须功能（MVP）
- [ ] Ctrl+P 打开/关闭性能监控面板
- [ ] 显示工作流基本信息
- [ ] 显示执行时间指标
- [ ] 显示百分位数统计
- [ ] Tab 键切换工作流
- [ ] Esc 关闭面板

---

## 📋 实施步骤

### 准备工作
```bash
# 1. 创建新分支
git checkout -b feature/tui-performance-monitor

# 2. 确认环境
cd v2
cargo test --workspace  # 确保所有测试通过
```

### Day 1: 基础组件（3-4 小时）
**任务**:
1. 创建 `v2/crates/agent-tui/src/ui/performance_overlay.rs`
2. 实现 `PerformanceOverlay` 结构
3. 集成到 `TuiApp`
4. 添加 Ctrl+P 快捷键

**参考**:
- `v2/crates/agent-tui/src/ui/subagent_overlay.rs` (模板)
- `v2/crates/agent-workflow/src/workflow/performance.rs` (API)

**验收**:
```bash
cargo test -p agent-tui performance_overlay
cargo run --release --bin agent-tui
# 按 Ctrl+P 应能看到空面板
```

### Day 2: 数据绑定和渲染（4-5 小时）
**任务**:
1. 实现数据获取逻辑
2. 设计 UI 布局
3. 实现 Ratatui 渲染
4. 添加状态颜色

**验收**:
```bash
# 执行一个工作流后，按 Ctrl+P 应看到实时数据
```

### Day 3: 交互功能（3-4 小时）
**任务**:
1. 实现多工作流切换
2. 完善键盘事件处理
3. 确保实时更新

**验收**:
```bash
# Tab 键可以切换工作流
# 数据实时更新
```

### Day 4: 测试和优化（2-3 小时）
**任务**:
1. 编写单元测试
2. 编写集成测试
3. 性能优化
4. 文档更新

**验收**:
```bash
cargo test -p agent-tui
cargo clippy -p agent-tui
cargo fmt -p agent-tui --check
```

### 最终验收
使用 `TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md` 进行手动验收测试。

**最低验收标准**:
- [ ] 场景 1: 面板可以打开和关闭
- [ ] 场景 2: 显示实时指标
- [ ] 场景 3: 显示百分位数
- [ ] 场景 8: 基本快捷键工作

---

## 🚀 快速启动指令

### 开始开发
```bash
# 1. 创建开发分支
git checkout main
git pull origin main
git checkout -b feature/tui-performance-monitor

# 2. 创建新文件
touch v2/crates/agent-tui/src/ui/performance_overlay.rs

# 3. 开始编码（参考 TUI_PERFORMANCE_MONITOR_PLAN.md）

# 4. 测试
cargo test -p agent-tui
cargo run --release --bin agent-tui
```

### 提交工作
```bash
# 每完成一天的工作
git add .
git commit -m "feat(tui): Day 1 - add PerformanceOverlay structure"

# 最终提交
git commit -m "feat(tui): integrate performance monitoring panel

- Add PerformanceOverlay component with Ctrl+P hotkey
- Display workflow metrics (duration, throughput, percentiles)
- Support multi-workflow switching with Tab
- Add comprehensive tests and documentation

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"

git push origin feature/tui-performance-monitor
```

### 合并到 Main
```bash
git checkout main
git merge feature/tui-performance-monitor --no-ff
git push origin main
```

---

## 📚 关键文档参考

### 实施指南
- **TUI_PERFORMANCE_MONITOR_PLAN.md** - 详细实施计划
- **TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md** - 验收测试步骤

### 代码参考
- `v2/crates/agent-tui/src/ui/subagent_overlay.rs` - Overlay 模板
- `v2/crates/agent-workflow/src/workflow/performance.rs` - 性能监控 API
- `v2/crates/agent-tui/src/app.rs` - TuiApp 主结构

### 项目文档
- `SESSION_HANDOFF.md` - 项目交接文档
- `MERGE_COMPLETE.md` - Subagent 合并完成报告

---

## 🎯 完成后的下一步

### 选项 1: 继续 V2 Rust 开发
**任务**:
- Workflow 深度集成（Subagent + Performance）
- API 服务器实现
- 更多 TUI 功能

**预计时间**: 2-3 周

### 选项 2: 开始 V3 C# TUI 开发
**任务**:
- 创建 .NET 8 项目
- 集成 Terminal.Gui
- 实现基础功能

**预计时间**: 4-5 周

**推荐**: 根据 SESSION_HANDOFF.md，建议完成当前短期计划（1.5-2 周）后再切换到 v3。

---

## ⚠️ 注意事项

### 开发注意事项
1. **参考 SubagentOverlay**: 它是一个很好的模板
2. **使用 Arc<Mutex<>>**: 确保线程安全
3. **避免阻塞**: 使用 `try_recv()` 而不是 `recv()`
4. **测试覆盖**: 每个新函数都应有测试

### 测试注意事项
1. **手动测试**: 定期运行 TUI 查看实际效果
2. **性能测试**: 确保不影响 TUI 响应速度
3. **边界情况**: 测试无工作流、失败工作流等情况

### Git 注意事项
1. **小提交**: 每完成一个功能就提交
2. **清晰消息**: 使用 conventional commits 格式
3. **Co-Authored-By**: 记得添加 Claude 的标注

---

## 📊 项目进度跟踪

### 短期计划进度（SESSION_HANDOFF.md）

1. **Subagent 系统完善** ✅
   - [x] 基础并行执行
   - [x] 状态管理
   - [x] TUI 集成
   - [ ] 与 Workflow 深度集成（可选）

2. **TUI 性能监控集成** ⏳ (当前任务)
   - [ ] 性能监控 UI 组件
   - [ ] 数据绑定
   - [ ] 交互功能
   - [ ] 测试和优化

### 中期计划（4-5 周后）

3. **v3 C# TUI 开发** 📋
   - [ ] 项目初始化
   - [ ] 核心功能
   - [ ] TUI 界面
   - [ ] 优化和发布

---

## 🆘 需要帮助？

### 遇到技术问题
1. 查看 `TUI_PERFORMANCE_MONITOR_PLAN.md` 的技术细节部分
2. 参考现有代码（SubagentOverlay）
3. 查看 Ratatui 官方文档：https://docs.rs/ratatui

### 遇到设计问题
1. 查看 `performance.rs` 了解可用的 API
2. 参考 `subagent_overlay.rs` 的布局设计
3. 使用 `colors.rs` 中的颜色方案

### 测试失败
1. 使用 `cargo test -- --nocapture` 查看输出
2. 使用 `eprintln!()` 调试（不影响 TUI）
3. 检查 Mutex 锁是否正确释放

---

## ✨ 成功标准

### 开发完成
- [ ] 所有代码已提交
- [ ] 所有测试通过
- [ ] 文档已更新
- [ ] 代码质量检查通过（clippy + fmt）

### 验收完成
- [ ] 手动验收测试通过（至少核心 4 个场景）
- [ ] 无严重 bug
- [ ] 性能符合要求
- [ ] 用户体验流畅

### 合并完成
- [ ] 合并到 main 分支
- [ ] 推送到 origin/main
- [ ] 更新 ROADMAP.md
- [ ] 创建完成报告

---

**当前状态**: 准备就绪，可以开始开发
**下一个命令**: `git checkout -b feature/tui-performance-monitor`

🚀 **祝开发顺利！**
