# 计划任务用户指南

## 简介

计划任务功能允许您创建定时执行的任务，支持两种调度方式：
- **Cron 表达式** - 标准的 5 字段 Cron 语法
- **自然语言** - 中文时间表达式（如"每天9:00"、"每周五下午5点"）

## 快速开始

### 1. 创建任务

```bash
# 使用 Cron 表达式（每天早上 9 点）
agent task schedule "每日报告" \
  --schedule "0 9 * * *" \
  --type custom \
  --payload '{"command":"echo Daily Report"}' \
  --description "每天生成报告"

# 使用自然语言（每周五下午 5 点）
agent task schedule "周报提醒" \
  --schedule "每周五下午5点" \
  --type reminder \
  --payload '{"message":"本周工作总结"}' \
  --description "周五提醒写周报"
```

### 2. 查看任务列表

```bash
# 查看所有任务
agent task list

# 按状态过滤
agent task list --status pending
agent task list --status paused

# 按类型过滤
agent task list --type skill
agent task list --type reminder

# JSON 格式输出
agent task list --format json
```

### 3. 查看任务详情

```bash
# 使用完整 ID
agent task show a1b2c3d4-e5f6-7890-abcd-1234567890ab

# 使用 ID 前缀（前 8 位）
agent task show a1b2c3d4
```

### 4. 管理任务

```bash
# 暂停任务
agent task pause a1b2c3d4

# 恢复任务
agent task resume a1b2c3d4

# 更新任务
agent task update a1b2c3d4 \
  --schedule "0 10 * * *" \
  --description "新的描述"

# 删除任务（带确认）
agent task delete a1b2c3d4

# 强制删除（跳过确认）
agent task delete a1b2c3d4 --force
```

### 5. 手动执行任务

```bash
# 立即执行任务（不等待下次调度时间）
agent task run a1b2c3d4
```

### 6. 查看执行历史

```bash
# 查看最近 20 条执行记录
agent task history a1b2c3d4

# 限制返回数量
agent task history a1b2c3d4 --limit 50

# JSON 格式输出
agent task history a1b2c3d4 --format json
```

## 任务类型

### 1. Skill Invocation（技能调用）

执行系统中已注册的技能。

```bash
agent task schedule "定时技能" \
  --schedule "0 9 * * *" \
  --type skill \
  --payload '{"Skill":"greeting","Args":{"user_name":"Alice"}}'
```

### 2. Memory Reminder（记忆提醒）

创建定时提醒。

```bash
agent task schedule "会议提醒" \
  --schedule "每周一上午9点" \
  --type reminder \
  --payload '{"Message":"团队周会"}'
```

### 3. Custom Command（自定义命令）

执行自定义命令或脚本。

```bash
agent task schedule "备份任务" \
  --schedule "0 2 * * *" \
  --type custom \
  --payload '{"Command":"backup.sh"}'
```

## 调度表达式

### Cron 表达式格式

5 字段格式：`分钟 小时 日 月 星期`

```
*    *    *    *    *
┬    ┬    ┬    ┬    ┬
│    │    │    │    └─ 星期 (0-6, 0=周日)
│    │    │    └────── 月份 (1-12)
│    │    └─────────── 日 (1-31)
│    └──────────────── 小时 (0-23)
└───────────────────── 分钟 (0-59)
```

**常用示例：**

| 表达式 | 说明 |
|--------|------|
| `0 9 * * *` | 每天 9:00 |
| `30 14 * * *` | 每天 14:30 |
| `0 9 * * 1` | 每周一 9:00 |
| `0 9 1 * *` | 每月 1 号 9:00 |
| `*/30 * * * *` | 每 30 分钟 |
| `0 9-17 * * 1-5` | 工作日 9:00-17:00 每小时 |

### 自然语言表达式

**支持的模式：**

1. **每天系列**
   - `每天9:00` - 每天早上 9 点
   - `每天 17:30` - 每天下午 5 点 30 分
   - `每天早上9点` - 早上 9 点
   - `每天下午5点` - 下午 5 点（17:00）
   - `每天晚上8点` - 晚上 8 点（20:00）

2. **每周系列**
   - `每周一9:00` - 每周一早上 9 点
   - `每周五17:00` - 每周五下午 5 点
   - `每周一早上9点` - 周一早上 9 点
   - `每周五下午5点` - 周五下午 5 点

3. **每月系列**
   - `每月1号9:00` - 每月 1 号早上 9 点
   - `每月15号 20:00` - 每月 15 号晚上 8 点

4. **间隔系列**
   - `每小时` - 每小时执行
   - `每30分钟` - 每 30 分钟执行
   - `每5分钟` - 每 5 分钟执行

## 高级选项

### 重试和超时

```bash
agent task schedule "需要重试的任务" \
  --schedule "0 9 * * *" \
  --type custom \
  --payload '{"Command":"flaky-task.sh"}' \
  --retries 3 \          # 最大重试 3 次
  --timeout 600          # 超时 600 秒（10 分钟）
```

### 时间范围限制

```bash
agent task schedule "限时任务" \
  --schedule "0 9 * * *" \
  --type reminder \
  --payload '{"Message":"提醒"}' \
  --start-at "2024-01-01T00:00:00" \    # 从 2024 年 1 月 1 日开始
  --end-at "2024-12-31T23:59:59"        # 到 2024 年 12 月 31 日结束
```

## 任务状态

| 状态 | 说明 |
|------|------|
| `Pending` | 等待执行 |
| `Paused` | 已暂停 |
| `Completed` | 已完成（无下次执行） |
| `Failed` | 执行失败 |

## 执行状态

| 状态 | 说明 |
|------|------|
| `Running` | 正在执行 |
| `Completed` | 执行成功 |
| `Failed` | 执行失败（达到最大重试次数） |
| `Timeout` | 执行超时 |
| `Cancelled` | 执行被取消 |

## 重试策略

任务执行失败时会自动重试，采用指数退避策略：
- 第 1 次重试：等待 2 秒
- 第 2 次重试：等待 4 秒
- 第 3 次重试：等待 8 秒
- ...
- 第 n 次重试：等待 2^n 秒

## 配置

在 `appsettings.json` 中配置：

```json
{
  "ScheduledTasks": {
    "DatabasePath": "scheduled_tasks.db",
    "ScanIntervalSeconds": 60,
    "MaxConcurrentTasks": 10
  }
}
```

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `DatabasePath` | `scheduled_tasks.db` | 数据库文件路径 |
| `ScanIntervalSeconds` | `60` | 任务扫描间隔（秒） |
| `MaxConcurrentTasks` | `10` | 最大并发任务数 |

## 常见问题

### 1. 任务没有按时执行

**原因：** 后台服务未启动或调度器已停止。

**解决：** 确保应用程序正在运行，后台服务会自动启动调度器。

### 2. 任务执行失败

**排查步骤：**
1. 使用 `agent task history <ID>` 查看执行历史
2. 检查错误消息
3. 验证任务负载（TaskPayload）是否正确

### 3. 自然语言表达式无法解析

**支持的格式有限：** 请参考"自然语言表达式"部分的支持模式列表。如果表达式复杂，建议使用 Cron 表达式。

### 4. 如何停止正在执行的任务

目前无法直接停止正在执行的任务，但可以：
1. 暂停任务（`agent task pause <ID>`）- 阻止下次执行
2. 等待当前执行完成或超时

## 最佳实践

1. **使用描述性名称** - 便于识别任务用途
2. **设置合理的超时时间** - 避免任务长时间挂起
3. **配置适当的重试次数** - 对于不稳定的操作增加重试
4. **定期检查执行历史** - 及时发现和修复问题
5. **使用时间范围限制** - 对于临时任务设置结束时间
6. **避免过于频繁的调度** - 考虑系统负载

## 示例场景

### 场景 1：每日数据备份

```bash
agent task schedule "数据备份" \
  --schedule "0 2 * * *" \
  --type custom \
  --payload '{"Command":"/scripts/backup.sh"}' \
  --description "每天凌晨 2 点备份数据" \
  --retries 2 \
  --timeout 3600
```

### 场景 2：工作日早会提醒

```bash
agent task schedule "早会提醒" \
  --schedule "0 9 * * 1-5" \
  --type reminder \
  --payload '{"Message":"团队早会即将开始"}' \
  --description "工作日早上 9 点提醒"
```

### 场景 3：定期技能执行

```bash
agent task schedule "定期报告" \
  --schedule "每周五下午5点" \
  --type skill \
  --payload '{"Skill":"generate-report","Args":{"type":"weekly"}}' \
  --description "每周五生成周报"
```

### 场景 4：临时监控任务

```bash
agent task schedule "临时监控" \
  --schedule "每5分钟" \
  --type custom \
  --payload '{"Command":"health-check.sh"}' \
  --description "项目发布期间的健康检查" \
  --start-at "2024-06-01T00:00:00" \
  --end-at "2024-06-07T23:59:59"
```

## 相关命令参考

完整的命令列表和参数说明，请运行：

```bash
agent task --help
agent task schedule --help
agent task list --help
# ... 其他命令
```
