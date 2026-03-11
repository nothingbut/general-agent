# 工作流通知系统

**功能概述:** 多渠道通知管理，支持终端、桌面等多种通知方式  
**模块:** `src/workflow/notification.py`

---

## 功能特性

### 1. 多渠道支持
- 终端通知 (TerminalChannel) - Rich 显示
- 桌面通知 (DesktopChannel) - 跨平台系统通知
- Web 通知 - 预留接口

### 2. 优先级管理
- CRITICAL - 删除、执行命令
- HIGH - 写入文件、更新数据
- NORMAL - 读取文件、查询
- LOW - 低优先级操作

---

## API 参考

详细文档见代码注释和测试文件。

**测试:** 33 个测试，100% 覆盖率 ✅
