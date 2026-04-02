# TUI 性能监控集成 - 实施计划

**目标**: 在 TUI 中添加性能监控面板，实时显示工作流性能指标
**预计时间**: 3-4 天
**优先级**: P1

---

## 📋 概述

### 现状
- ✅ 性能监控框架已完成：`v2/crates/agent-workflow/src/workflow/performance.rs`
- ✅ TUI 基础框架已完成：`v2/crates/agent-tui/`
- ✅ Subagent Overlay 已实现（参考模板）

### 目标
在 TUI 中添加一个新的 Overlay 组件，显示工作流性能指标。

---

## 🎯 核心功能需求

### 必须功能（MVP）
1. **面板显示**
   - Ctrl+P 打开/关闭性能监控面板
   - Overlay 形式显示（类似 SubagentOverlay）

2. **基本指标**
   - 工作流 ID 和状态
   - 总耗时（毫秒）
   - 已完成/总任务数
   - 吞吐量（任务/秒）

3. **统计指标**
   - 平均任务执行时间
   - P50/P95/P99 百分位数

4. **键盘控制**
   - Esc 关闭面板
   - Tab 切换不同工作流

### 可选功能（V2）
- 资源使用显示（内存/CPU）
- 性能报告导出（E 键）
- 历史数据查看
- 实时图表（简单柱状图）

---

## 🏗️ 架构设计

### 组件结构
```
v2/crates/agent-tui/src/ui/
├── mod.rs                    # 添加 PerformanceOverlay 导出
├── performance_overlay.rs    # 新建：性能监控 Overlay
└── ...（其他 UI 组件）
```

### 数据流
```
PerformanceMonitor (agent-workflow)
         ↓
   [共享状态/通道]
         ↓
 PerformanceOverlay (agent-tui)
         ↓
   [Ratatui 渲染]
         ↓
      用户界面
```

### API 集成
```rust
// 在 TuiApp 中添加
pub struct TuiApp {
    // ... 现有字段
    performance_overlay: PerformanceOverlay,
    performance_monitor: Arc<Mutex<PerformanceMonitor>>,
}
```

---

## 📝 实施步骤

### Day 1: 基础组件（3-4 小时）

#### 1.1 创建 PerformanceOverlay 结构
**文件**: `v2/crates/agent-tui/src/ui/performance_overlay.rs`

```rust
pub struct PerformanceOverlay {
    visible: bool,
    selected_workflow_index: usize,
    monitor: Arc<Mutex<PerformanceMonitor>>,
}

impl PerformanceOverlay {
    pub fn new(monitor: Arc<Mutex<PerformanceMonitor>>) -> Self;
    pub fn toggle_visibility(&mut self);
    pub fn is_visible(&self) -> bool;
    pub fn render(&self, frame: &mut Frame, area: Rect);
    pub fn handle_key_event(&mut self, key: KeyEvent) -> bool;
}
```

#### 1.2 集成到 TuiApp
**文件**: `v2/crates/agent-tui/src/app.rs`

- 添加 `performance_overlay` 字段
- 在 `draw()` 中调用 `performance_overlay.render()`
- 在 `handle_events()` 中添加 Ctrl+P 处理

#### 1.3 基础测试
```bash
cargo test -p agent-tui performance_overlay
```

---

### Day 2: 数据绑定和渲染（4-5 小时）

#### 2.1 实现数据获取逻辑
```rust
impl PerformanceOverlay {
    fn get_workflow_list(&self) -> Vec<String> {
        // 从 PerformanceMonitor 获取所有工作流 ID
    }

    fn get_current_metrics(&self) -> Option<WorkflowMetrics> {
        // 获取当前选中工作流的指标
    }
}
```

#### 2.2 实现 UI 渲染
**布局**:
```
┌─ 性能监控 [1/3] ─────────────────┐
│ 工作流: wf-abc123                │
│ 状态: ● Running                  │
│                                  │
│ ┌─ 执行指标 ────────────────────┐│
│ │ 总耗时: 1234ms                ││
│ │ 任务: 5/10 (50%)              ││
│ │ 吞吐量: 8.1 任务/秒           ││
│ └───────────────────────────────┘│
│                                  │
│ ┌─ 任务时间统计 ────────────────┐│
│ │ 平均: 45ms                    ││
│ │ P50:  42ms                    ││
│ │ P95:  78ms                    ││
│ │ P99:  92ms                    ││
│ └───────────────────────────────┘│
│                                  │
│ [Tab] 切换  [Esc] 关闭           │
└──────────────────────────────────┘
```

使用 Ratatui 组件:
- `Block` + `Borders` 创建边框
- `Paragraph` 显示文本
- `Style` 应用颜色（参考 `colors.rs`）

#### 2.3 状态颜色
```rust
fn get_status_color(status: &str) -> Color {
    match status {
        "pending" => Color::Yellow,
        "running" => Color::Blue,
        "completed" => Color::Green,
        "failed" => Color::Red,
        _ => Color::Gray,
    }
}
```

---

### Day 3: 交互功能（3-4 小时）

#### 3.1 多工作流切换
```rust
impl PerformanceOverlay {
    fn next_workflow(&mut self) {
        let workflows = self.get_workflow_list();
        if !workflows.is_empty() {
            self.selected_workflow_index =
                (self.selected_workflow_index + 1) % workflows.len();
        }
    }

    fn prev_workflow(&mut self) {
        let workflows = self.get_workflow_list();
        if !workflows.is_empty() {
            self.selected_workflow_index =
                self.selected_workflow_index
                    .checked_sub(1)
                    .unwrap_or(workflows.len() - 1);
        }
    }
}
```

#### 3.2 键盘事件处理
**文件**: `v2/crates/agent-tui/src/app.rs`

```rust
// 在 handle_events() 中添加
if key.modifiers.contains(KeyModifiers::CONTROL) {
    match key.code {
        KeyCode::Char('p') => {
            self.performance_overlay.toggle_visibility();
            return Ok(true);
        }
        // ...
    }
}

// 如果 overlay 可见，优先处理其事件
if self.performance_overlay.is_visible() {
    if self.performance_overlay.handle_key_event(key) {
        return Ok(true);
    }
}
```

#### 3.3 实时更新
确保 PerformanceMonitor 在任务执行时更新：
```rust
// 在工作流执行时
monitor.start_workflow(workflow_id, total_tasks);
// ... 执行任务
monitor.start_task(task_id, task_name, workflow_id);
// ... 任务完成
monitor.complete_task(task_id, status, duration);
```

---

### Day 4: 测试和优化（2-3 小时）

#### 4.1 单元测试
```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_overlay_toggle() {
        let monitor = Arc::new(Mutex::new(PerformanceMonitor::new()));
        let mut overlay = PerformanceOverlay::new(monitor);

        assert!(!overlay.is_visible());
        overlay.toggle_visibility();
        assert!(overlay.is_visible());
    }

    #[test]
    fn test_workflow_switching() {
        // 测试多工作流切换逻辑
    }

    #[test]
    fn test_metrics_display() {
        // 测试指标格式化
    }
}
```

#### 4.2 集成测试
创建 `v2/crates/agent-tui/tests/performance_overlay_test.rs`

#### 4.3 性能优化
- 缓存工作流列表（避免频繁查询）
- 使用 `try_recv` 避免阻塞
- 渲染优化（仅在数据变化时重绘）

#### 4.4 文档更新
- 更新 `v2/crates/agent-tui/README.md`
- 添加快捷键说明
- 添加截图（可选）

---

## 🔧 技术细节

### 依赖关系
```toml
# v2/crates/agent-tui/Cargo.toml
[dependencies]
agent-workflow = { path = "../agent-workflow" }
ratatui = "0.26"
crossterm = "0.27"
tokio = { version = "1", features = ["full"] }
```

### 线程安全
使用 `Arc<Mutex<PerformanceMonitor>>` 在 TUI 线程和后台任务间共享。

### 错误处理
```rust
pub enum OverlayError {
    MonitorLockFailed,
    NoWorkflowData,
    InvalidWorkflowId,
}

pub type OverlayResult<T> = Result<T, OverlayError>;
```

---

## 📊 代码估算

| 组件 | 文件 | 预计行数 | 时间 |
|------|------|----------|------|
| PerformanceOverlay 结构 | performance_overlay.rs | ~150 行 | 2h |
| 渲染逻辑 | performance_overlay.rs | ~200 行 | 3h |
| 键盘处理 | performance_overlay.rs | ~80 行 | 1h |
| TuiApp 集成 | app.rs | ~50 行 | 1h |
| 测试 | tests/ | ~150 行 | 2h |
| 文档 | README.md 等 | ~100 行 | 1h |
| **总计** | | **~730 行** | **10h** |

---

## ✅ 验收标准

### 功能验收
- [ ] Ctrl+P 可以打开/关闭性能监控面板
- [ ] 显示工作流基本信息（ID、状态、任务数）
- [ ] 显示执行时间指标（总耗时、吞吐量、平均时间）
- [ ] 显示百分位数（P50/P95/P99）
- [ ] Tab 键可以切换不同工作流
- [ ] Esc 可以关闭面板
- [ ] 指标实时更新

### 代码质量
- [ ] 所有测试通过（cargo test -p agent-tui）
- [ ] 无编译警告（cargo clippy）
- [ ] 代码格式正确（cargo fmt）
- [ ] 文档完整（README + 注释）

### 性能要求
- [ ] Overlay 打开时间 < 50ms
- [ ] 渲染帧率 > 30 FPS
- [ ] 内存占用 < 10MB（额外）
- [ ] CPU 使用率 < 5%（idle 时）

---

## 🔄 参考实现

### 参考 SubagentOverlay
**文件**: `v2/crates/agent-tui/src/ui/subagent_overlay.rs`

关键学习点：
1. Overlay 可见性管理
2. Ratatui 渲染流程
3. 键盘事件处理
4. 与 TuiApp 集成方式

### 参考 PerformanceMonitor API
**文件**: `v2/crates/agent-workflow/src/workflow/performance.rs`

关键 API：
- `start_workflow(id, total_tasks)`
- `complete_workflow(id)`
- `get_workflow_metrics(id)`
- `WorkflowMetrics` 结构

---

## 📚 开发指南

### 开发流程
1. 创建新分支: `git checkout -b feature/tui-performance-monitor`
2. 实施 Day 1 任务
3. 提交: `git commit -m "feat(tui): add PerformanceOverlay structure"`
4. 继续 Day 2-4...
5. 最终合并到 main

### 测试命令
```bash
# 运行单元测试
cargo test -p agent-tui

# 运行集成测试
cargo test -p agent-tui --test performance_overlay_test

# 手动测试
cargo run --release --bin agent-tui

# 代码检查
cargo clippy -p agent-tui
cargo fmt -p agent-tui --check
```

### 调试技巧
1. 使用 `eprintln!()` 输出到 stderr（不影响 TUI）
2. 创建日志文件: `log::info!()` + `env_logger`
3. 使用 `dbg!()` 宏快速调试

---

## 🎉 完成后

### 文档更新
- [ ] 更新 `TUI_PERFORMANCE_MONITOR_ACCEPTANCE.md` 的实际结果
- [ ] 更新 `v2/crates/agent-tui/README.md` 添加性能监控说明
- [ ] 在 `ROADMAP.md` 中标记完成

### 提交和合并
```bash
git add .
git commit -m "feat(tui): integrate performance monitoring panel

- Add PerformanceOverlay component with Ctrl+P hotkey
- Display workflow metrics (duration, throughput, percentiles)
- Support multi-workflow switching with Tab
- Add comprehensive tests and documentation

Closes: TUI Performance Monitor integration task

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>"

git push origin feature/tui-performance-monitor
# 然后合并到 main
```

### 下一步
完成后可以继续：
1. V2 功能：导出性能报告
2. V3 功能：历史数据查看
3. 或者切换到 v3 C# TUI 开发

---

**计划创建日期**: 2026-03-14
**预计开始日期**: ___________
**预计完成日期**: ___________
