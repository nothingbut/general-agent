# General Agent V3 (C# - Planned)

这个目录为未来的 C# 实现版本预留。

## 规划状态

🚧 **计划中** - 尚未开始开发

## 预期特性

C# 版本将提供：
- **.NET 8+** 支持
- **跨平台** (Windows, Linux, macOS)
- **高性能** async/await 模型
- **企业级** 集成能力
- **完整的 IDE 支持** (Visual Studio, Rider)

## 核心功能

### Tool Calling

Tool Calling 是 General Agent V3 的核心特性，允许 LLM 自动调用工具完成复杂任务。

**特点**：
- **智能调用**：LLM 根据对话内容自动选择合适的工具
- **多轮对话**：支持连续多轮工具调用，直到任务完成
- **用户确认**：达到限制时可选择继续或停止
- **安全保护**：防止无限循环和资源滥用

**快速开始**：

```bash
# 显式调用（@ 语法）
@greeting user_name='张三'
@personal:reminder task='买牛奶' time='5pm'

# 隐式调用（自然语言）
用户：帮我向张三问好
Agent：[自动调用 @greeting user_name='张三']
```

**配置示例**：

```json
{
  "ToolCalling": {
    "Enabled": true,
    "MaxRounds": 3,
    "InteractiveMode": true,
    "AutoExtendBy": 5,
    "AbsoluteMaxRounds": 20
  }
}
```

📖 **详细文档**：[Tool Calling 使用指南](./docs/tool-calling.md)

## 目标场景

- Windows 企业环境集成
- Azure 云原生部署
- .NET 生态系统整合
- 企业级应用开发

## 开发时间线

待定 - 将在 V2 (Rust) 稳定后启动

## 参考实现

开发时将参考：
- **V1 (Python)** - API 设计和功能完整性
- **V2 (Rust)** - 性能优化和架构模式

## 相关文档

- [技能系统指南](./docs/SKILLS_GUIDE.md) - 如何创建和使用技能
- [Tool Calling 指南](./docs/tool-calling.md) - Tool Calling 完整文档
- [技能示例](./skills/README.md) - 预定义技能集合

---

**注意**: 当前项目重点在 V2 (Rust) 开发。V3 的具体规划将在 V2 达到生产就绪状态后确定。
