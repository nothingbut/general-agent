# 上下文压缩系统实施计划

**功能**: 上下文压缩系统
**优先级**: ⭐⭐⭐⭐⭐ (P0)
**预计耗时**: 5 个工作日
**开始日期**: 2026-04-15
**结束日期**: 2026-04-19
**状态**: 📋 待开始

---

## 🎯 目标

实现 3 种上下文压缩策略，防止长对话 Token 超限，提升用户体验。

---

## 📋 功能需求

### 核心功能
1. **Token 计数**
   - 支持多种模型（Claude、Qwen 等）
   - 准确率 > 95%

2. **滑动窗口策略**
   - 保留系统消息
   - 保留最近 N 条消息
   - 可配置窗口大小

3. **语义压缩策略**
   - LLM 生成摘要
   - 关键信息保留（日期、名称、数字）
   - 上下文连贯性

4. **分层压缩策略**
   - 多级压缩（粗粒度 → 细粒度）
   - 智能策略选择

5. **自动触发**
   - 消息数 >= 15 自动压缩
   - 手动触发支持

---

## 🏗️ 架构设计

### Crate 结构

```
agent-context-compression/
├── Cargo.toml
├── src/
│   ├── lib.rs                    # 公共接口
│   ├── token_counter.rs          # Token 计数
│   ├── strategies/
│   │   ├── mod.rs                # 策略 trait 定义
│   │   ├── sliding_window.rs    # 滑动窗口策略
│   │   ├── semantic.rs           # 语义压缩策略
│   │   └── hierarchical.rs      # 分层压缩策略
│   ├── service.rs                # CompressionService 主服务
│   ├── error.rs                  # 错误类型
│   └── models.rs                 # 数据模型
└── tests/
    ├── token_counter_tests.rs
    ├── sliding_window_tests.rs
    ├── semantic_tests.rs
    ├── hierarchical_tests.rs
    └── integration_tests.rs
```

### 核心接口

```rust
// src/strategies/mod.rs
use async_trait::async_trait;
use agent_core::{Message, Result};

/// 压缩策略 trait
#[async_trait]
pub trait CompressionStrategy: Send + Sync {
    /// 压缩消息列表
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>>;
    
    /// 策略名称
    fn name(&self) -> &str;
    
    /// 估算压缩后的 token 数
    fn estimate_tokens(&self, messages: &[Message]) -> usize;
}

/// 策略类型枚举
pub enum StrategyType {
    SlidingWindow,
    Semantic,
    Hierarchical,
}
```

### 数据模型

```rust
// src/models.rs
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

/// 压缩历史记录
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompressionRecord {
    pub id: Uuid,
    pub session_id: Uuid,
    pub strategy: String,
    pub original_message_count: usize,
    pub compressed_message_count: usize,
    pub original_tokens: usize,
    pub compressed_tokens: usize,
    pub compression_ratio: f64,
    pub created_at: DateTime<Utc>,
}

/// 压缩配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CompressionConfig {
    /// 自动压缩触发阈值（消息数）
    pub auto_trigger_threshold: usize,
    
    /// 滑动窗口大小
    pub sliding_window_size: usize,
    
    /// 语义压缩目标 token 数
    pub semantic_target_tokens: usize,
    
    /// 是否启用自动压缩
    pub auto_compression_enabled: bool,
}

impl Default for CompressionConfig {
    fn default() -> Self {
        Self {
            auto_trigger_threshold: 15,
            sliding_window_size: 10,
            semantic_target_tokens: 2000,
            auto_compression_enabled: true,
        }
    }
}
```

---

## 📅 实施计划

### Day 1 (2026-04-15): Token 计数基础

#### 任务清单
- [x] 创建 `agent-context-compression` crate
- [ ] 添加依赖
- [ ] 实现 `TokenCounter` 结构体
- [ ] 支持 Claude 模型（`claude-3-5-sonnet-20241022`）
- [ ] 支持 Qwen 模型
- [ ] 单元测试（5-10 个）

#### 技术细节

**Cargo.toml**
```toml
[package]
name = "agent-context-compression"
version = "0.1.0"
edition = "2021"

[dependencies]
# Workspace dependencies
agent-core = { path = "../agent-core" }
agent-llm = { path = "../agent-llm" }

# Token counting
tiktoken-rs = "0.5"

# Async
tokio = { workspace = true }
async-trait = { workspace = true }

# Serialization
serde = { workspace = true }
serde_json = { workspace = true }

# Error handling
thiserror = { workspace = true }
anyhow = { workspace = true }

# UUID
uuid = { workspace = true }

# Time
chrono = { workspace = true }

# Logging
tracing = { workspace = true }

[dev-dependencies]
mockito = { workspace = true }
```

**src/token_counter.rs**
```rust
use tiktoken_rs::{cl100k_base, get_bpe_from_model};
use anyhow::Result;

/// Token 计数器
pub struct TokenCounter {
    bpe: tiktoken_rs::CoreBPE,
}

impl TokenCounter {
    /// 为 Claude 模型创建计数器
    pub fn new_for_claude() -> Result<Self> {
        // Claude 使用 cl100k_base tokenizer
        let bpe = cl100k_base()?;
        Ok(Self { bpe })
    }
    
    /// 为指定模型创建计数器
    pub fn new_for_model(model: &str) -> Result<Self> {
        let bpe = if model.starts_with("claude") {
            cl100k_base()?
        } else if model.starts_with("qwen") {
            // Qwen 也使用 cl100k_base
            cl100k_base()?
        } else {
            // 默认使用 cl100k_base
            cl100k_base()?
        };
        Ok(Self { bpe })
    }
    
    /// 计算文本的 token 数
    pub fn count(&self, text: &str) -> usize {
        self.bpe.encode_with_special_tokens(text).len()
    }
    
    /// 计算消息的 token 数
    pub fn count_message(&self, message: &agent_core::Message) -> usize {
        // Format: role + content + overhead (4 tokens per message)
        let role_tokens = self.count(&message.role);
        let content_tokens = self.count(&message.content);
        role_tokens + content_tokens + 4
    }
    
    /// 计算消息列表的总 token 数
    pub fn count_messages(&self, messages: &[agent_core::Message]) -> usize {
        messages.iter().map(|m| self.count_message(m)).sum()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use agent_core::Message;
    
    #[test]
    fn test_count_simple_text() {
        let counter = TokenCounter::new_for_claude().unwrap();
        let count = counter.count("Hello, world!");
        assert!(count > 0);
        assert!(count < 10);  // 应该是 3-4 个 token
    }
    
    #[test]
    fn test_count_message() {
        let counter = TokenCounter::new_for_claude().unwrap();
        let message = Message {
            role: "user".to_string(),
            content: "Hello, world!".to_string(),
            ..Default::default()
        };
        let count = counter.count_message(&message);
        assert!(count > 5);  // role + content + overhead
    }
    
    #[test]
    fn test_count_messages() {
        let counter = TokenCounter::new_for_claude().unwrap();
        let messages = vec![
            Message {
                role: "user".to_string(),
                content: "Hello".to_string(),
                ..Default::default()
            },
            Message {
                role: "assistant".to_string(),
                content: "Hi there!".to_string(),
                ..Default::default()
            },
        ];
        let count = counter.count_messages(&messages);
        assert!(count > 10);
    }
}
```

#### 验收标准
- ✅ 能准确计算文本的 token 数
- ✅ 能计算消息的 token 数
- ✅ 能计算消息列表的总 token 数
- ✅ 所有单元测试通过

---

### Day 2 (2026-04-16): 滑动窗口策略

#### 任务清单
- [ ] 实现 `SlidingWindowStrategy` 结构体
- [ ] 保留系统消息（role = "system"）
- [ ] 保留最近 N 条消息
- [ ] 可配置窗口大小
- [ ] 单元测试（5-10 个）

#### 技术细节

**src/strategies/sliding_window.rs**
```rust
use super::CompressionStrategy;
use crate::token_counter::TokenCounter;
use agent_core::{Message, Result};
use async_trait::async_trait;

/// 滑动窗口压缩策略
pub struct SlidingWindowStrategy {
    window_size: usize,
    token_counter: TokenCounter,
}

impl SlidingWindowStrategy {
    /// 创建新的滑动窗口策略
    pub fn new(window_size: usize) -> Result<Self> {
        let token_counter = TokenCounter::new_for_claude()?;
        Ok(Self {
            window_size,
            token_counter,
        })
    }
}

#[async_trait]
impl CompressionStrategy for SlidingWindowStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>> {
        if messages.len() <= self.window_size {
            // 不需要压缩
            return Ok(messages.to_vec());
        }
        
        // 分离系统消息和非系统消息
        let (system_msgs, other_msgs): (Vec<_>, Vec<_>) = messages
            .iter()
            .partition(|m| m.role == "system");
        
        // 保留系统消息 + 最近 N 条消息
        let keep_count = self.window_size.saturating_sub(system_msgs.len());
        let kept_msgs: Vec<Message> = other_msgs
            .into_iter()
            .rev()
            .take(keep_count)
            .rev()
            .cloned()
            .collect();
        
        // 合并：系统消息在前，最近消息在后
        let mut result = system_msgs.into_iter().cloned().collect::<Vec<_>>();
        result.extend(kept_msgs);
        
        Ok(result)
    }
    
    fn name(&self) -> &str {
        "SlidingWindow"
    }
    
    fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.token_counter.count_messages(messages)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    
    fn create_message(role: &str, content: &str) -> Message {
        Message {
            role: role.to_string(),
            content: content.to_string(),
            ..Default::default()
        }
    }
    
    #[tokio::test]
    async fn test_compress_under_threshold() {
        let strategy = SlidingWindowStrategy::new(10).unwrap();
        let messages = vec![
            create_message("user", "msg 1"),
            create_message("assistant", "msg 2"),
        ];
        
        let compressed = strategy.compress(&messages).await.unwrap();
        assert_eq!(compressed.len(), 2);
    }
    
    #[tokio::test]
    async fn test_compress_over_threshold() {
        let strategy = SlidingWindowStrategy::new(5).unwrap();
        let messages = vec![
            create_message("system", "You are a helpful assistant"),
            create_message("user", "msg 1"),
            create_message("assistant", "msg 2"),
            create_message("user", "msg 3"),
            create_message("assistant", "msg 4"),
            create_message("user", "msg 5"),
            create_message("assistant", "msg 6"),
        ];
        
        let compressed = strategy.compress(&messages).await.unwrap();
        
        // 应该保留系统消息 + 最近 4 条
        assert_eq!(compressed.len(), 5);
        assert_eq!(compressed[0].role, "system");
        assert_eq!(compressed[1].content, "msg 3");
        assert_eq!(compressed[4].content, "msg 6");
    }
}
```

#### 验收标准
- ✅ 能正确保留系统消息
- ✅ 能保留最近 N 条消息
- ✅ 压缩后消息顺序正确
- ✅ 所有单元测试通过

---

### Day 3 (2026-04-17): 语义压缩策略

#### 任务清单
- [ ] 实现 `SemanticStrategy` 结构体
- [ ] LLM 生成摘要
- [ ] 关键信息保留
- [ ] 上下文连贯性检查
- [ ] 单元测试（5-10 个）

#### 技术细节

**src/strategies/semantic.rs**
```rust
use super::CompressionStrategy;
use crate::token_counter::TokenCounter;
use agent_core::{Message, Result};
use agent_llm::LlmClient;
use async_trait::async_trait;
use std::sync::Arc;

const COMPRESSION_SYSTEM_PROMPT: &str = r#"你是一个专业的对话压缩助手。
你的任务是将一段对话历史压缩成简洁的摘要，同时保留以下关键信息：
1. 重要的事实、日期、名称、数字
2. 用户的主要问题和需求
3. 助手的核心建议和解决方案
4. 对话的上下文和逻辑流程

请用第三人称的叙述方式生成摘要，保持客观和准确。"#;

/// 语义压缩策略
pub struct SemanticStrategy {
    llm_client: Arc<LlmClient>,
    token_counter: TokenCounter,
    target_tokens: usize,
}

impl SemanticStrategy {
    /// 创建新的语义压缩策略
    pub fn new(llm_client: Arc<LlmClient>, target_tokens: usize) -> Result<Self> {
        let token_counter = TokenCounter::new_for_claude()?;
        Ok(Self {
            llm_client,
            token_counter,
            target_tokens,
        })
    }
}

#[async_trait]
impl CompressionStrategy for SemanticStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>> {
        // 分离系统消息和对话消息
        let (system_msgs, dialog_msgs): (Vec<_>, Vec<_>) = messages
            .iter()
            .partition(|m| m.role == "system");
        
        // 如果对话消息很少，不需要压缩
        if dialog_msgs.len() <= 2 {
            return Ok(messages.to_vec());
        }
        
        // 格式化对话历史
        let dialog_text = dialog_msgs
            .iter()
            .map(|m| format!("{}: {}", m.role, m.content))
            .collect::<Vec<_>>()
            .join("\n\n");
        
        // 构造压缩提示
        let compression_prompt = format!(
            "请将以下对话历史压缩成简洁的摘要（目标长度约 {} tokens）：\n\n{}",
            self.target_tokens, dialog_text
        );
        
        // 调用 LLM 生成摘要
        let summary_messages = vec![
            Message {
                role: "system".to_string(),
                content: COMPRESSION_SYSTEM_PROMPT.to_string(),
                ..Default::default()
            },
            Message {
                role: "user".to_string(),
                content: compression_prompt,
                ..Default::default()
            },
        ];
        
        let summary_response = self.llm_client.chat(&summary_messages).await?;
        
        // 构造压缩后的消息列表
        let mut result = system_msgs.into_iter().cloned().collect::<Vec<_>>();
        result.push(Message {
            role: "assistant".to_string(),
            content: format!("[对话摘要]\n{}", summary_response),
            ..Default::default()
        });
        
        Ok(result)
    }
    
    fn name(&self) -> &str {
        "Semantic"
    }
    
    fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.token_counter.count_messages(messages)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use mockito;
    
    // 测试需要 mock LLM 客户端
    // 这里只展示结构，具体实现需要使用 mockito
    
    #[tokio::test]
    async fn test_compress_with_mock_llm() {
        // TODO: 使用 mockito 创建 mock LLM 服务器
        // 验证压缩逻辑
    }
}
```

#### 验收标准
- ✅ 能调用 LLM 生成摘要
- ✅ 摘要保留关键信息
- ✅ 压缩后 token 数接近目标值
- ✅ 所有单元测试通过

---

### Day 4 (2026-04-18): 分层压缩策略

#### 任务清单
- [ ] 实现 `HierarchicalStrategy` 结构体
- [ ] 多级压缩逻辑
- [ ] 智能策略选择
- [ ] 单元测试（5-10 个）

#### 技术细节

**src/strategies/hierarchical.rs**
```rust
use super::{CompressionStrategy, SlidingWindowStrategy, SemanticStrategy};
use agent_core::{Message, Result};
use agent_llm::LlmClient;
use async_trait::async_trait;
use std::sync::Arc;

/// 分层压缩策略
pub struct HierarchicalStrategy {
    sliding_window: SlidingWindowStrategy,
    semantic: SemanticStrategy,
    token_threshold: usize,
}

impl HierarchicalStrategy {
    /// 创建新的分层压缩策略
    pub fn new(
        llm_client: Arc<LlmClient>,
        window_size: usize,
        token_threshold: usize,
    ) -> Result<Self> {
        Ok(Self {
            sliding_window: SlidingWindowStrategy::new(window_size)?,
            semantic: SemanticStrategy::new(llm_client, 2000)?,
            token_threshold,
        })
    }
}

#[async_trait]
impl CompressionStrategy for HierarchicalStrategy {
    async fn compress(&self, messages: &[Message]) -> Result<Vec<Message>> {
        let token_count = self.semantic.estimate_tokens(messages);
        
        // 根据 token 数选择策略
        if token_count < self.token_threshold {
            // 消息较少，使用滑动窗口
            self.sliding_window.compress(messages).await
        } else {
            // 消息较多，使用语义压缩
            self.semantic.compress(messages).await
        }
    }
    
    fn name(&self) -> &str {
        "Hierarchical"
    }
    
    fn estimate_tokens(&self, messages: &[Message]) -> usize {
        self.semantic.estimate_tokens(messages)
    }
}
```

---

### Day 5 (2026-04-19): 集成和测试

#### 任务清单
- [ ] 实现 `CompressionService` 主服务
- [ ] 自动触发机制
- [ ] `ConversationFlow` 集成
- [ ] CLI 命令
- [ ] 集成测试（5-10 个）
- [ ] 文档更新

#### 技术细节

**src/service.rs**
```rust
use crate::strategies::{CompressionStrategy, StrategyType};
use crate::models::{CompressionConfig, CompressionRecord};
use agent_core::{Message, Result};
use agent_storage::Repository;
use std::sync::Arc;
use uuid::Uuid;

/// 压缩服务
pub struct CompressionService {
    config: CompressionConfig,
    strategies: std::collections::HashMap<StrategyType, Arc<dyn CompressionStrategy>>,
    repository: Arc<dyn Repository<CompressionRecord>>,
}

impl CompressionService {
    /// 压缩消息列表
    pub async fn compress(
        &self,
        session_id: Uuid,
        messages: &[Message],
        strategy_type: StrategyType,
    ) -> Result<Vec<Message>> {
        let strategy = self.strategies.get(&strategy_type)
            .ok_or_else(|| anyhow::anyhow!("Strategy not found"))?;
        
        let original_tokens = strategy.estimate_tokens(messages);
        let compressed = strategy.compress(messages).await?;
        let compressed_tokens = strategy.estimate_tokens(&compressed);
        
        // 记录压缩历史
        let record = CompressionRecord {
            id: Uuid::new_v4(),
            session_id,
            strategy: strategy.name().to_string(),
            original_message_count: messages.len(),
            compressed_message_count: compressed.len(),
            original_tokens,
            compressed_tokens,
            compression_ratio: compressed_tokens as f64 / original_tokens as f64,
            created_at: chrono::Utc::now(),
        };
        
        self.repository.create(&record).await?;
        
        Ok(compressed)
    }
    
    /// 检查是否需要自动压缩
    pub fn should_auto_compress(&self, message_count: usize) -> bool {
        self.config.auto_compression_enabled
            && message_count >= self.config.auto_trigger_threshold
    }
}
```

**CLI 命令**（集成到 `agent-cli`）
```rust
// agent-cli/src/commands/compress.rs
use clap::Args;

#[derive(Args, Debug)]
pub struct CompressCommand {
    /// 会话 ID
    session_id: String,
    
    /// 压缩策略
    #[arg(long, default_value = "sliding-window")]
    strategy: String,
}

pub async fn execute(cmd: CompressCommand) -> anyhow::Result<()> {
    // 实现压缩命令
    println!("压缩会话 {}", cmd.session_id);
    // TODO: 调用 CompressionService
    Ok(())
}
```

#### 验收标准
- ✅ 能手动触发压缩
- ✅ 能自动触发压缩
- ✅ 压缩历史正确记录
- ✅ CLI 命令正常工作
- ✅ 所有集成测试通过

---

## ✅ 验收标准

### 功能验收
- ✅ 3 种压缩策略全部实现
- ✅ Token 计数准确（误差 < 5%）
- ✅ 自动触发机制正常
- ✅ CLI 命令完整可用
- ✅ 与 ConversationFlow 集成成功

### 性能验收
- ✅ 滑动窗口压缩: < 10ms
- ✅ 语义压缩: < 2s (LLM 调用)
- ✅ Token 计数: < 1ms (单条消息)

### 质量验收
- ✅ 20+ 个单元测试全部通过
- ✅ 5+ 个集成测试全部通过
- ✅ 代码覆盖率 > 80%
- ✅ 无编译警告

### 文档验收
- ✅ API 文档完整（Rust Doc）
- ✅ 使用示例清晰
- ✅ 配置说明详细

---

## 🚀 下一步

完成上下文压缩系统后，立即开始**长期记忆系统**（Week 3-4）。

---

**最后更新**: 2026-04-15
**版本**: Context Compression Implementation Plan v1.0
