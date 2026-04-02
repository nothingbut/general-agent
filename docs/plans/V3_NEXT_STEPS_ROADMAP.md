# General Agent V3 - 下一步开发计划和路线图

**创建日期**: 2026-03-25
**当前版本**: v3.0.0 - Phoenix
**状态**: ✅ V3.0.0 已发布，规划 V3.1 及后续版本

---

## 📋 执行摘要

V3.0.0 已成功完成并发布，包含完整的核心功能、CLI 工具、REPL 系统和性能优化。接下来的开发将聚焦于增强用户体验、添加协作功能和高级分析能力。

**优先级排序**:
1. **V3.1** (P0) - 快速迭代改进（2-3 周）
2. **Phase 6** (P1) - TUI 模式增强（3-4 周）
3. **Phase 7** (P1) - 协作功能（4-5 周）
4. **Phase 8** (P2) - 高级分析（3-4 周）

**总体时间线**: V3.1 预计 4 月中旬，Phase 6-7 预计 6 月完成

---

## 🎯 V3.1 - 快速迭代改进（优先级 P0）

**时间线**: 2-3 周
**目标**: 基于用户反馈的快速改进和增强

### 功能清单

#### 1. 消息搜索功能
**优先级**: P0
**工作量**: 3 天

**功能描述**:
- 在会话历史中搜索消息内容
- 支持关键词、正则表达式、模糊搜索
- 高亮显示搜索结果
- 搜索结果导出

**技术方案**:
```csharp
// MessageSearchService.cs
public class MessageSearchService
{
    public async Task<List<SearchResult>> SearchMessages(
        string query,
        SearchType type = SearchType.Keyword,
        string? sessionId = null,
        int limit = 50
    );
}
```

**验收标准**:
- [ ] 支持关键词搜索
- [ ] 支持正则表达式搜索
- [ ] 支持模糊搜索（Levenshtein 距离）
- [ ] 搜索响应时间 < 300ms
- [ ] 测试覆盖率 > 85%

---

#### 2. 会话标签系统
**优先级**: P0
**工作量**: 4 天

**功能描述**:
- 为会话添加多个标签
- 按标签过滤和搜索会话
- 标签自动建议
- 标签统计和管理

**技术方案**:
```csharp
// 数据库模式更新
CREATE TABLE session_tags (
    session_id TEXT NOT NULL,
    tag TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (session_id, tag),
    FOREIGN KEY (session_id) REFERENCES sessions(id)
);

// SessionTagService.cs
public class SessionTagService
{
    public Task AddTag(string sessionId, string tag);
    public Task RemoveTag(string sessionId, string tag);
    public Task<List<string>> GetTags(string sessionId);
    public Task<List<Session>> GetSessionsByTag(string tag);
    public Task<Dictionary<string, int>> GetTagStatistics();
}
```

**CLI 命令**:
```bash
/tag add <session-id> <tag>          # 添加标签
/tag remove <session-id> <tag>       # 移除标签
/tag list <session-id>               # 列出会话标签
/list --tag <tag>                    # 按标签过滤会话
/tags                                # 标签统计
```

**验收标准**:
- [ ] 支持添加/删除标签
- [ ] 支持按标签过滤
- [ ] 标签自动补全
- [ ] 标签统计功能
- [ ] 测试覆盖率 > 85%

---

#### 3. 配置文件管理
**优先级**: P1
**工作量**: 3 天

**功能描述**:
- 多配置文件支持（开发、生产、测试）
- 配置文件切换
- 配置验证和迁移
- 配置导入/导出

**技术方案**:
```bash
~/.agent/
├── config/
│   ├── default.json        # 默认配置
│   ├── development.json    # 开发配置
│   ├── production.json     # 生产配置
│   └── custom.json         # 自定义配置
└── active_config.txt       # 当前激活的配置
```

**CLI 命令**:
```bash
/config list                         # 列出所有配置
/config switch <name>                # 切换配置
/config show                         # 显示当前配置
/config export <file>                # 导出配置
/config import <file>                # 导入配置
```

**验收标准**:
- [ ] 支持多配置文件
- [ ] 配置切换无需重启
- [ ] 配置验证（JSON Schema）
- [ ] 测试覆盖率 > 85%

---

#### 4. 主题系统
**优先级**: P2
**工作量**: 2 天

**功能描述**:
- 多种颜色主题（dark, light, dracula, nord 等）
- 自定义颜色方案
- 主题预览和切换
- 持久化主题设置

**预定义主题**:
```json
{
  "dark": {
    "primary": "cyan",
    "success": "green",
    "error": "red",
    "warning": "yellow",
    "info": "blue"
  },
  "light": {
    "primary": "blue",
    "success": "green",
    "error": "red",
    "warning": "yellow",
    "info": "cyan"
  },
  "dracula": {
    "primary": "purple",
    "success": "green",
    "error": "red",
    "warning": "yellow",
    "info": "cyan"
  }
}
```

**CLI 命令**:
```bash
/theme list                          # 列出主题
/theme set <name>                    # 设置主题
/theme preview <name>                # 预览主题
/theme custom <json>                 # 自定义主题
```

**验收标准**:
- [ ] 3+ 预定义主题
- [ ] 自定义主题支持
- [ ] 主题预览功能
- [ ] 测试覆盖率 > 80%

---

### V3.1 总结

**预期成果**:
- 4 个新功能模块
- ~1,800 行新代码
- ~1,200 行测试代码
- 85%+ 测试覆盖率

**发布目标**:
- 2026 年 4 月中旬
- 完整的文档更新
- 迁移指南

---

## 🚀 Phase 6 - TUI 模式增强（优先级 P1）

**时间线**: 3-4 周
**目标**: 提供高级 TUI 交互体验

### 6.1 分屏布局
**工作量**: 5 天

**功能描述**:
- 使用 Spectre.Console Live Display 实现分屏
- 左侧：会话列表 + 技能列表
- 右侧：对话区域
- 底部：输入框 + 状态栏
- 可调整分屏比例

**技术方案**:
```
┌─────────────────────────────────────────────┐
│ General Agent V3 - TUI Mode                 │
├──────────────┬──────────────────────────────┤
│  会话列表    │  当前会话: 测试会话          │
│  ▸ 测试会话  │  ─────────────────────────   │
│    项目讨论  │  You> 你好                   │
│    代码评审  │  Assistant> 你好！有什么我   │
│              │  可以帮助你的吗？            │
│  技能列表    │                              │
│  📚 personal │  You> 请解释一下技能系统     │
│  ⚙️ system   │  Assistant> 技能系统是...   │
│              │                              │
│              │                              │
├──────────────┴──────────────────────────────┤
│ You> _                                      │
│ 💡 提示: Tab 补全, ↑↓ 历史, Ctrl+Q 退出   │
└─────────────────────────────────────────────┘
```

**快捷键**:
- `Ctrl+Tab`: 切换焦点（会话列表 ↔ 对话区域）
- `Ctrl+N`: 新建会话
- `Ctrl+S`: 保存会话
- `Ctrl+Q`: 退出 TUI

**验收标准**:
- [ ] 分屏布局实现
- [ ] 实时更新（Live Display）
- [ ] 快捷键支持
- [ ] 响应时间 < 100ms

---

### 6.2 实时状态监控
**工作量**: 3 天

**功能描述**:
- LLM 请求状态（Pending/In Progress/Complete）
- Token 使用统计（当前会话 + 总计）
- 技能执行状态
- 系统资源监控（CPU/内存）

**状态栏设计**:
```
┌─────────────────────────────────────────────┐
│ 🟢 Claude Sonnet 4.5 | 📊 Tokens: 1.2K/200K │
│ ⏱️  响应: 1.5s | 💾 内存: 55MB | 🔄 缓存: ✓  │
└─────────────────────────────────────────────┘
```

**验收标准**:
- [ ] 实时状态更新
- [ ] Token 使用统计
- [ ] 性能监控集成
- [ ] 不影响 REPL 响应时间

---

### 6.3 键盘导航增强
**工作量**: 2 天

**功能描述**:
- Vim 风格的键盘导航（可选）
- 快速跳转（会话、技能、命令）
- 键盘快捷键自定义
- 快捷键帮助面板（Ctrl+?）

**快捷键方案**:
```json
{
  "global": {
    "quit": "Ctrl+Q",
    "help": "Ctrl+?",
    "clear": "Ctrl+L"
  },
  "navigation": {
    "next_session": "Ctrl+Down",
    "prev_session": "Ctrl+Up",
    "switch_panel": "Ctrl+Tab"
  },
  "vim_mode": {
    "enabled": false,
    "bindings": {
      "h": "left",
      "j": "down",
      "k": "up",
      "l": "right"
    }
  }
}
```

**验收标准**:
- [ ] 完整键盘导航
- [ ] 快捷键自定义
- [ ] Vim 模式支持（可选）
- [ ] 帮助面板

---

### Phase 6 总结

**预期成果**:
- 高级 TUI 交互体验
- ~2,000 行新代码
- ~1,500 行测试代码
- 完整的键盘导航系统

**发布目标**:
- 2026 年 5 月中旬
- TUI 使用指南
- 视频演示

---

## 🤝 Phase 7 - 协作功能（优先级 P1）

**时间线**: 4-5 周
**目标**: 支持团队协作和会话分享

### 7.1 会话导入/导出
**工作量**: 3 天

**功能描述**:
- 导出会话为 JSON/Markdown
- 导入会话（保留或重新生成 ID）
- 批量导入/导出
- 会话归档

**导出格式**:
```json
{
  "version": "3.0.0",
  "session": {
    "id": "abc123",
    "title": "技术讨论",
    "created_at": "2026-03-25T10:00:00Z",
    "tags": ["tech", "ai"],
    "messages": [
      {
        "role": "user",
        "content": "你好",
        "timestamp": "2026-03-25T10:00:01Z"
      }
    ]
  }
}
```

**CLI 命令**:
```bash
/export <session-id> <file>          # 导出会话
/export all <directory>              # 导出所有会话
/import <file>                       # 导入会话
/import batch <directory>            # 批量导入
```

**验收标准**:
- [ ] 支持 JSON 和 Markdown 格式
- [ ] 保留完整元数据
- [ ] 导入冲突处理
- [ ] 测试覆盖率 > 85%

---

### 7.2 团队技能库
**工作量**: 5 天

**功能描述**:
- 远程技能仓库（Git 集成）
- 技能共享和订阅
- 技能版本管理
- 技能评分和评论

**目录结构**:
```
~/.agent/
├── skills/                # 本地技能
│   ├── personal/
│   └── productivity/
└── team_skills/           # 团队技能（Git 仓库）
    ├── .git/
    ├── engineering/
    ├── marketing/
    └── README.md
```

**CLI 命令**:
```bash
/skills remote add <name> <url>      # 添加远程仓库
/skills remote list                  # 列出远程仓库
/skills sync                         # 同步技能
/skills subscribe <namespace>        # 订阅命名空间
/skills publish <skill>              # 发布技能
```

**验收标准**:
- [ ] Git 集成
- [ ] 自动同步
- [ ] 版本管理
- [ ] 冲突解决
- [ ] 测试覆盖率 > 80%

---

### 7.3 远程数据库支持
**工作量**: 6 天

**功能描述**:
- 支持 PostgreSQL 和 MySQL
- 数据库连接池
- 自动迁移和同步
- 多设备数据同步

**配置示例**:
```json
{
  "storage": {
    "type": "postgresql",
    "connection": {
      "host": "localhost",
      "port": 5432,
      "database": "general_agent",
      "username": "user",
      "password": "pass",
      "ssl": true
    },
    "pool": {
      "min": 2,
      "max": 10
    }
  }
}
```

**技术方案**:
- 使用 Npgsql (PostgreSQL) 和 MySql.Data (MySQL)
- Repository 模式抽象
- 数据库无关的查询 DSL
- 自动 Schema 迁移

**验收标准**:
- [ ] 支持 PostgreSQL 和 MySQL
- [ ] 连接池管理
- [ ] 自动迁移
- [ ] 性能不低于 SQLite
- [ ] 测试覆盖率 > 85%

---

### 7.4 会话分享
**工作量**: 4 天

**功能描述**:
- 生成会话分享链接
- 只读会话访问
- 访问权限控制（公开/团队/私有）
- 分享统计

**分享链接示例**:
```
https://agent.example.com/share/abc123?token=xyz
```

**CLI 命令**:
```bash
/share <session-id> [--public|--team|--private]
/share list                          # 列出分享的会话
/share revoke <session-id>           # 撤销分享
/share stats <session-id>            # 分享统计
```

**验收标准**:
- [ ] 生成分享链接
- [ ] 权限控制
- [ ] 访问统计
- [ ] 分享过期时间
- [ ] 测试覆盖率 > 80%

---

### Phase 7 总结

**预期成果**:
- 完整的协作功能体系
- ~2,500 行新代码
- ~1,800 行测试代码
- 协作功能文档

**发布目标**:
- 2026 年 6 月中旬
- 协作功能指南
- 团队使用案例

---

## 📊 Phase 8 - 高级分析（优先级 P2）

**时间线**: 3-4 周
**目标**: 提供使用分析和洞察

### 8.1 使用统计
**工作量**: 4 天

**功能描述**:
- Token 使用统计和趋势
- 会话活动分析
- 技能使用频率
- 用户活动热图

**统计维度**:
- 每日/周/月 Token 使用量
- 会话创建和活跃度
- 技能调用频率
- LLM 提供商使用分布
- 响应时间分布

**报告格式**:
```
📊 General Agent 使用统计 (2026-03)

Token 使用:
  总计: 1.2M tokens
  平均: 40K tokens/天
  峰值: 85K tokens (2026-03-15)

会话活动:
  总会话: 245
  活跃会话: 12
  平均消息数: 18/会话

技能使用 TOP 5:
  1. personal:greeting (342 次)
  2. productivity:task (156 次)
  3. code:review (89 次)
  4. research:search (67 次)
  5. personal:reminder (45 次)
```

**CLI 命令**:
```bash
/stats                               # 总体统计
/stats sessions                      # 会话统计
/stats skills                        # 技能统计
/stats tokens                        # Token 统计
/stats export <file>                 # 导出统计报告
```

**验收标准**:
- [ ] 完整的统计指标
- [ ] 多维度分析
- [ ] 趋势图表（Spectre.Console）
- [ ] 导出功能
- [ ] 测试覆盖率 > 80%

---

### 8.2 会话分析
**工作量**: 5 天

**功能描述**:
- 会话总结生成
- 关键话题提取
- 情感分析
- 建议和洞察

**分析报告示例**:
```markdown
# 会话分析报告: 技术讨论

## 基本信息
- 会话ID: abc123
- 消息数: 45
- 持续时间: 2.5 小时
- Token 使用: 12.3K

## 话题分布
1. 架构设计 (40%)
2. 性能优化 (30%)
3. 测试策略 (20%)
4. 部署方案 (10%)

## 关键决策
- 决定使用微服务架构
- 选择 Redis 作为缓存
- 采用 Docker 部署

## 待办事项
- [ ] 完成架构设计文档
- [ ] 搭建开发环境
- [ ] 编写单元测试
```

**CLI 命令**:
```bash
/analyze <session-id>                # 分析会话
/analyze summary                     # 会话总结
/analyze topics                      # 话题提取
/analyze sentiment                   # 情感分析
```

**验收标准**:
- [ ] 自动会话总结
- [ ] 话题提取算法
- [ ] 情感分析集成
- [ ] Markdown 报告生成
- [ ] 测试覆盖率 > 75%

---

### 8.3 技能使用分析
**工作量**: 3 天

**功能描述**:
- 技能使用热图
- 技能效果评估
- 技能推荐系统
- 技能优化建议

**热图示例**:
```
技能使用热图 (最近 30 天)

          Mon Tue Wed Thu Fri Sat Sun
Week 1    ██  ███ ███ ██  █   ░   ░
Week 2    ███ ███ ██  ███ ██  █   ░
Week 3    ██  ███ ███ ██  ███ █   ░
Week 4    ███ ██  ███ ███ ██  ██  █

图例: ░ 0-5次  █ 6-10次  ██ 11-20次  ███ 20+次
```

**CLI 命令**:
```bash
/skills heatmap                      # 使用热图
/skills recommendations              # 推荐技能
/skills insights                     # 使用洞察
```

**验收标准**:
- [ ] 使用热图生成
- [ ] 推荐算法
- [ ] 洞察生成
- [ ] 测试覆盖率 > 75%

---

### Phase 8 总结

**预期成果**:
- 完整的分析功能
- ~1,800 行新代码
- ~1,200 行测试代码
- 分析功能文档

**发布目标**:
- 2026 年 7 月
- 分析功能指南
- 最佳实践文档

---

## 🔮 长期规划（Phase 9+）

### Phase 9: 插件系统
**时间线**: 4-5 周

**功能**:
- 插件开发 SDK
- 插件市场
- 插件沙箱
- 插件权限管理

---

### Phase 10: API 服务
**时间线**: 3-4 周

**功能**:
- RESTful API
- GraphQL 端点
- WebSocket 支持
- API 文档（Swagger）

---

### Phase 11: Web 界面
**时间线**: 5-6 周

**功能**:
- React/Blazor Web UI
- 实时协作
- 可视化编辑器
- 移动端适配

---

## 📊 总体时间线

```
2026年
├── 4月
│   └── V3.1 发布 ✨
├── 5月
│   ├── Phase 6 Chunk 1-2 完成
│   └── Phase 6 发布 (TUI 增强) 🎨
├── 6月
│   ├── Phase 7 Chunk 1-3 完成
│   └── Phase 7 发布 (协作功能) 🤝
├── 7月
│   └── Phase 8 发布 (高级分析) 📊
└── 8月+
    └── Phase 9-11 (插件、API、Web)
```

---

## 🎯 成功指标

### V3.1
- [ ] 4 个新功能全部实现
- [ ] 测试覆盖率 > 85%
- [ ] 用户满意度 > 90%
- [ ] 零重大 Bug

### Phase 6
- [ ] TUI 模式完整实现
- [ ] 响应时间 < 100ms
- [ ] 内存占用 < 80MB
- [ ] 完整的键盘导航

### Phase 7
- [ ] 协作功能完整
- [ ] 多设备同步稳定
- [ ] 远程数据库性能达标
- [ ] 团队技能库活跃

### Phase 8
- [ ] 分析功能准确
- [ ] 报告生成速度 < 5s
- [ ] 洞察有价值
- [ ] 用户采用率 > 70%

---

## 🚨 风险和缓解

### 技术风险

1. **TUI 性能问题**
   - 风险: Spectre.Console Live Display 可能影响性能
   - 缓解: 使用增量更新，优化渲染逻辑

2. **远程数据库复杂性**
   - 风险: 多数据库支持增加复杂度
   - 缓解: Repository 模式抽象，充分测试

3. **技能同步冲突**
   - 风险: Git 合并冲突难以自动解决
   - 缓解: 提供冲突解决 UI，手动介入

### 资源风险

1. **开发时间不足**
   - 缓解: 优先级排序，可选功能降级

2. **测试覆盖不足**
   - 缓解: 每个 Phase 都包含测试任务

---

## 📝 参考资料

### V3.0.0 文档
- [发布说明](RELEASE_NOTES_V3.0.0.md)
- [项目总结](V3_PROJECT_SUMMARY.md)
- [UAT 报告](V3_UAT_REPORT.md)
- [CLI 指南](v3/docs/CLI_GUIDE.md)

### 技术参考
- [Spectre.Console](https://spectreconsole.net/)
- [Npgsql](https://www.npgsql.org/)
- [MySql.Data](https://dev.mysql.com/doc/connector-net/en/)

---

## ✅ 行动项

### 立即执行（本周）
- [ ] 创建 V3.1 GitHub Milestone
- [ ] 创建功能 Issue (消息搜索、标签系统等)
- [ ] 设置项目看板

### 近期执行（本月）
- [ ] 开始 V3.1 开发
- [ ] 收集用户反馈
- [ ] 优化现有功能

### 中期执行（2-3 月）
- [ ] Phase 6 详细设计
- [ ] TUI 原型开发
- [ ] Phase 7 需求调研

---

**路线图创建**: 2026-03-25
**创建者**: Claude Sonnet 4.5
**下次更新**: 每月或重大里程碑后

---

**祝 General Agent V3 未来发展顺利！🎉**
