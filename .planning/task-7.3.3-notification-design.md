# Task 7.3.3: 审批通知系统设计

**日期:** 2026-03-08
**状态:** 设计中

---

## 目标

实现灵活的通知系统，在审批请求创建时通知用户。

---

## 核心功能

### 1. 通知管理器（NotificationManager）

- 管理通知渠道
- 发送通知到多个渠道
- 记录通知历史
- 通知优先级管理

### 2. 通知渠道（Notification Channels）

#### 终端通知（TerminalChannel）
- 使用 Rich 库显示
- 支持颜色和样式
- 适合 CLI 环境

#### 桌面通知（DesktopChannel）
- macOS: 使用 osascript
- Linux: 使用 notify-send
- Windows: 使用 PowerShell/win10toast
- 跨平台支持

#### Web 通知（WebChannel）
- 未来扩展
- 通过 WebSocket/SSE 推送
- 供 Web 界面使用

### 3. 通知优先级

- **CRITICAL** - 关键操作（删除、执行命令）
- **HIGH** - 重要操作（写入文件）
- **NORMAL** - 普通操作（读取文件）
- **LOW** - 低优先级操作

### 4. 通知历史

- 存储到数据库
- 支持查询和过滤
- 显示通知状态（已读/未读）

---

## 数据模型

```python
@dataclass
class Notification:
    id: str
    workflow_id: str
    task_id: str
    title: str
    message: str
    priority: NotificationPriority
    channels: List[str]  # ['terminal', 'desktop', 'web']
    created_at: datetime
    read: bool = False
```

---

## API 设计

```python
class NotificationManager:
    async def send(
        self,
        notification: Notification
    ) -> Dict[str, bool]:
        """发送通知到所有渠道

        Returns:
            {channel_name: success}
        """

    def register_channel(
        self,
        name: str,
        channel: NotificationChannel
    ):
        """注册通知渠道"""

    async def get_history(
        self,
        workflow_id: Optional[str] = None,
        limit: int = 50
    ) -> List[dict]:
        """获取通知历史"""
```

---

## 集成到审批流程

```python
# 在 ApprovalManager 中
async def request_approval(self, task, workflow):
    # 1. 创建审批请求
    request = ApprovalRequest(...)

    # 2. 发送通知
    notification = Notification(
        title="审批请求",
        message=f"任务 {task.name} 需要审批",
        priority=self._determine_priority(task),
        channels=['terminal', 'desktop']
    )
    await self.notification_manager.send(notification)

    # 3. 等待审批
    result = await self._wait_for_approval(request)

    return result
```

---

## 实施计划

### Phase 1: 核心架构
- [ ] Notification 模型
- [ ] NotificationPriority 枚举
- [ ] NotificationChannel 抽象基类
- [ ] NotificationManager 核心类

### Phase 2: 终端通知
- [ ] TerminalChannel 实现
- [ ] Rich 样式渲染
- [ ] 优先级颜色映射

### Phase 3: 桌面通知
- [ ] DesktopChannel 实现
- [ ] macOS 支持（osascript）
- [ ] Linux 支持（notify-send）
- [ ] Windows 支持（可选）

### Phase 4: 数据库集成
- [ ] 通知历史表
- [ ] 存储和查询接口
- [ ] 未读通知管理

### Phase 5: 测试和文档
- [ ] 单元测试（20+ 用例）
- [ ] 集成测试
- [ ] 使用文档
- [ ] 演示程序

---

## 技术选型

### 终端通知
- **Rich** - 已集成，用于美观显示

### 桌面通知
- **macOS**: `subprocess` + `osascript`
- **Linux**: `subprocess` + `notify-send`
- **Windows**: `win10toast` (可选)

不使用 `plyer` - 避免额外依赖，直接使用系统命令。

---

## 预期成果

- 通知管理器核心类
- 2+ 通知渠道实现（终端 + 桌面）
- 通知历史功能
- 20+ 测试用例
- 完整文档
- 演示程序

---

**估计工时:** 2-3 小时
