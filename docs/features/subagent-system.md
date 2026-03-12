# Subagent System

## 概述

Subagent System 是一个强大的并行任务执行系统，允许用户在单个会话中启动多个独立的子代理来并行处理不同的任务。

## 核心特性

### 1. 并行任务执行
- 每个子代理在独立的会话中运行
- 互不干扰，资源隔离
- 自动生成唯一的 session_id

### 2. 完整的生命周期管理
- **创建**: 通过 `/subagent start` 命令启动
- **监控**: 实时查看状态和进度
- **取消**: 支持优雅关闭（未来实现）
- **清理**: 自动释放资源

### 3. 可视化监控
- 实时状态显示（Pending/Running/Completed/Failed）
- 彩色状态指示器
- 支持两种视图模式
- 键盘快捷键导航

## 使用指南

### 基本命令

在 TUI 中启动子代理：

```bash
/subagent start "任务1" "任务2" "任务3"
```

示例：
```bash
/subagent start "分析这个代码库的架构" "生成API文档" "运行性能测试"
```

### 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+S` | 切换 Subagent Monitor 可见性 |
| `Tab` | 切换视图模式（CurrentSession ↔ Global） |
| `Up/Down` | 在列表中导航 |
| `Esc` | 关闭 Monitor |

### 视图模式

#### CurrentSession 视图
- 只显示当前会话的子代理
- 适合专注于单个项目

#### Global 视图
- 显示所有会话的子代理
- 适合跨会话监控

## 架构设计

### 核心组件

```
SubagentOrchestrator (src/core/subagent_orchestrator.rs)
    ├── 任务解析
    ├── 会话创建
    └── 状态管理

SubagentOverlay (src/tui/components/subagent_overlay.rs)
    ├── 状态渲染
    ├── 键盘事件处理
    └── 视图切换
```

### 数据流

```
User Input → CommandParser → SubagentOrchestrator
                                    ↓
                            Database (sessions表)
                                    ↓
                            SubagentOverlay (实时查询)
```

### 数据模型

子代理会话在数据库中的特征：
- `session_type = 'Subagent'`
- `parent_id = <父会话ID>`
- `status = 'pending' | 'running' | 'completed' | 'failed'`
- `metadata.task_description` = 任务描述

## 状态机

```
Pending → Running → Completed
              ↓
            Failed
```

- **Pending**: 已创建但未开始执行
- **Running**: 正在执行中
- **Completed**: 成功完成
- **Failed**: 执行失败

## 颜色编码

| 状态 | 颜色 | 含义 |
|------|------|------|
| Pending | Yellow | 等待执行 |
| Running | Blue | 正在运行 |
| Completed | Green | 成功完成 |
| Failed | Red | 执行失败 |

## 限制与注意事项

### 当前限制
1. **只读监控**: 暂不支持直接操作子代理（取消/暂停）
2. **无实时日志**: 不显示子代理的输出流
3. **无进度条**: 只有状态枚举，无百分比进度

### 最佳实践
1. **任务粒度**: 每个子任务应该是独立的、明确的
2. **资源考虑**: 避免同时启动过多子代理（建议 < 10 个）
3. **错误处理**: 监控 Failed 状态并手动检查错误原因

## 未来规划

### Phase 2: 高级控制
- [ ] 子代理取消功能
- [ ] 暂停/恢复支持
- [ ] 优先级设置

### Phase 3: 增强可视化
- [ ] 实时日志流
- [ ] 进度百分比
- [ ] 资源使用监控（CPU/内存）

### Phase 4: 智能调度
- [ ] 自动负载均衡
- [ ] 依赖任务编排
- [ ] 失败重试机制

## 故障排查

### 问题：Subagent Monitor 不显示任何内容
- **原因**: 当前会话没有子代理
- **解决**: 切换到 Global 视图（按 Tab）

### 问题：子代理状态一直是 Pending
- **原因**: 执行引擎未启动或任务队列阻塞
- **解决**: 检查日志，重启 TUI

### 问题：快捷键不响应
- **原因**: 焦点在其他组件上
- **解决**: 确保 Overlay 可见且焦点正确

## 技术细节

### 会话隔离
每个子代理有独立的：
- Session ID
- Context Manager
- Message History
- Database Records

### 性能优化
- 懒加载：只在打开 Overlay 时查询数据库
- 增量渲染：只更新变化的状态
- 异步查询：不阻塞主 UI 线程

### 安全性
- 子代理无法访问其他会话的数据
- 父会话可以查询但不能修改子会话
- 自动清理过期会话

## 示例场景

### 场景 1: 代码审查
```bash
/subagent start \
  "检查src/目录的代码风格" \
  "运行所有单元测试" \
  "生成测试覆盖率报告"
```

### 场景 2: 数据分析
```bash
/subagent start \
  "清洗data.csv数据" \
  "生成统计摘要" \
  "创建可视化图表"
```

### 场景 3: 文档生成
```bash
/subagent start \
  "生成API参考文档" \
  "更新README" \
  "检查断开的链接"
```

## 相关文档

- [TUI 使用指南](../tui.md)
- [SubagentOrchestrator 架构](../plans/2026-03-12-subagent-architecture.md)
- [数据库模式](../DATABASE_SCHEMA.md)
