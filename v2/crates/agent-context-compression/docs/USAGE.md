# 使用指南

本文档提供 `agent-context-compression` 的详细使用说明。

## 目录

- [基础概念](#基础概念)
- [快速开始](#快速开始)
- [压缩策略](#压缩策略)
- [配置管理](#配置管理)
- [Token 计数](#token-计数)
- [LRU 缓存](#lru-缓存)
- [高级用法](#高级用法)
- [示例代码](#示例代码)

---

## 基础概念

### 什么是上下文压缩？

在对话系统中，随着对话的进行，历史消息会不断累积，导致：
- **Token 使用量增加**：每次请求都需要发送完整历史
- **成本上升**：LLM API 按 Token 计费
- **响应变慢**：处理更多 Token 需要更多时间

上下文压缩通过智能算法减少历史消息数量，在保持对话连贯性的同时降低 Token 使用。

### 三种压缩策略

| 策略 | 原理 | 优点 | 缺点 | 适用场景 |
|-----|------|------|------|---------|
| **滑动窗口** | 保留最近 N 条消息 | 极快（~270 ps） | 信息丢失多 | 短对话、实时场景 |
| **语义压缩** | LLM 生成摘要 | 信息保留多 | 慢（需调用 LLM） | 长对话、归档 |
| **分层压缩** | 智能选择策略 | 平衡性能和效果 | 配置复杂 | 通用（推荐） |

---

## 快速开始

### 1. 添加依赖

在 `Cargo.toml` 中：

```toml
[dependencies]
agent-context-compression = { path = "../agent-context-compression" }
agent-llm = { path = "../agent-llm" }
agent-core = { path = "../agent-core" }
tokio = { version = "1.0", features = ["full"] }
anyhow = "1.0"
```

### 2. 基本使用

```rust
use agent_context_compression::{
    CompressionService, CompressionConfig, StrategyType
};
use agent_llm::anthropic::AnthropicClient;
use std::sync::Arc;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 创建 LLM 客户端
    let llm_client = Arc::new(AnthropicClient::new(
        std::env::var("ANTHROPIC_API_KEY")?
    ));
    
    // 创建压缩服务
    let config = CompressionConfig::default();
    let mut service = CompressionService::new(llm_client, config)?;
    
    // 假设有一些消息
    let messages = vec![/* ... */];
    
    // 自动压缩（根据阈值自动触发）
    let result = service.auto_compress(&messages).await?;
    
    println!("压缩结果:");
    println!("  原始消息数: {}", result.original_count);
    println!("  压缩后消息数: {}", result.compressed_count);
    println!("  压缩率: {:.2}%", result.compression_ratio * 100.0);
    println!("  节省 Token: {}", result.saved_tokens);
    
    Ok(())
}
```

### 3. 手动选择策略

```rust
// 使用滑动窗口（最快）
let result = service.compress(&messages, StrategyType::SlidingWindow).await?;

// 使用语义压缩（最省 Token）
let result = service.compress(&messages, StrategyType::Semantic).await?;

// 使用分层压缩（推荐）
let result = service.compress(&messages, StrategyType::Hierarchical).await?;
```

---

## 压缩策略

### 滑动窗口策略

**原理**: 保留最近的 N 条消息，丢弃旧消息。

**配置**:
```rust
let mut config = CompressionConfig::default();
config.sliding_window_size = 20;        // 保留最近 20 条
config.preserve_system_messages = true;  // 始终保留系统消息
```

**适用场景**:
- ✅ 短对话（< 50 条消息）
- ✅ 实时响应要求高
- ✅ Token 预算充足
- ❌ 需要保留完整历史

**性能**: ~270 ps（皮秒级，几乎即时）

### 语义压缩策略

**原理**: 使用 LLM 将多条消息总结成摘要。

**配置**:
```rust
config.semantic_model = "claude-3-5-sonnet-20241022".to_string();
config.preserve_system_messages = true;
```

**适用场景**:
- ✅ 长对话（> 100 条消息）
- ✅ 需要保留关键信息
- ✅ 归档/总结场景
- ❌ 实时响应要求高
- ❌ Token 预算紧张（需额外调用 LLM）

**性能**: 2-5 秒（取决于 LLM 响应速度）

### 分层压缩策略（推荐）

**原理**: 根据对话长度和 Token 数自动选择最优策略。

**决策逻辑**:
```
if 消息数 <= 20:
    使用滑动窗口（快速）
elif 消息数 <= 50:
    使用滑动窗口（保留 20 条）
else:
    if Token数 > 8000:
        使用语义压缩（省 Token）
    else:
        使用滑动窗口（快速）
```

**配置**:
```rust
config.hierarchical_small_threshold = 20;
config.hierarchical_large_threshold = 50;
config.hierarchical_token_threshold = 8000;
```

**适用场景**:
- ✅ 通用场景（推荐默认使用）
- ✅ 对话长度不确定
- ✅ 需要平衡性能和效果

---

## 配置管理

### 代码配置

```rust
use agent_context_compression::CompressionConfig;

let mut config = CompressionConfig {
    auto_compress: true,
    token_threshold: 8000,
    compression_history_limit: 50,
    default_strategy: StrategyType::Hierarchical,
    
    // 滑动窗口配置
    sliding_window_size: 20,
    preserve_system_messages: true,
    
    // 语义压缩配置
    semantic_model: "claude-3-5-sonnet-20241022".to_string(),
    
    // 分层压缩配置
    hierarchical_small_threshold: 20,
    hierarchical_large_threshold: 50,
    hierarchical_token_threshold: 8000,
};
```

### 文件配置

**TOML 格式** (`compression.toml`):

```toml
auto_compress = true
token_threshold = 8000
compression_history_limit = 50
default_strategy = "hierarchical"

[strategies.sliding_window]
window_size = 20
preserve_system = true

[strategies.semantic]
model = "claude-3-5-sonnet-20241022"
preserve_system = true

[strategies.hierarchical]
small_threshold = 20
large_threshold = 50
large_token_threshold = 8000
```

**YAML 格式** (`compression.yaml`):

```yaml
auto_compress: true
token_threshold: 8000
compression_history_limit: 50
default_strategy: hierarchical

strategies:
  sliding_window:
    window_size: 20
    preserve_system: true
    
  semantic:
    model: claude-3-5-sonnet-20241022
    preserve_system: true
    
  hierarchical:
    small_threshold: 20
    large_threshold: 50
    large_token_threshold: 8000
```

**加载配置**:

```rust
use agent_context_compression::ConfigFile;

// 从 TOML 加载
let config_file = ConfigFile::from_file("compression.toml")?;
let service = CompressionService::new(llm_client, config_file.config)?;

// 从 YAML 加载
let config_file = ConfigFile::from_file("compression.yaml")?;

// 保存配置
config_file.save("output.toml")?;
```

---

## Token 计数

### 基本使用

```rust
use agent_context_compression::TokenCounter;

// 创建计数器（Claude 模型）
let counter = TokenCounter::new_for_claude()?;

// 计数单条文本
let count = counter.count("Hello, world!");
println!("Token 数: {}", count);  // 输出: 4

// 计数消息
let message = Message { /* ... */ };
let count = counter.count_message(&message);

// 批量计数
let messages = vec![/* ... */];
let total = counter.count_messages(&messages);
```

### 支持的模型

```rust
// Claude 模型（默认）
let counter = TokenCounter::new_for_claude()?;

// 自定义模型
let counter = TokenCounter::new_for_model("gpt-4")?;
```

### 性能特征

| 文本长度 | 耗时 | 说明 |
|---------|------|------|
| 10 词 | 7.04 µs | 超快 |
| 100 词 | 67.02 µs | 快 |
| 1000 词 | 670.1 µs | 中等 |
| 5000 词 | 4.60 ms | 较慢 |

**优化建议**: 对于重复内容，使用 LRU 缓存（见下节）。

---

## LRU 缓存

### 为什么需要缓存？

在实际应用中，很多文本会重复出现（如系统提示词、常见问题），每次重新计数会浪费性能。使用 LRU 缓存可以：

- ⚡ **214x 性能提升**（缓存命中时）
- 💰 降低 CPU 使用
- 📈 提高吞吐量

### 基本使用

```rust
use agent_context_compression::{TokenCounter, Cache};

// 创建计数器和缓存
let counter = TokenCounter::new_for_claude()?;
let cache: Cache<String, usize> = Cache::new(1000);  // 容量 1000

// 带缓存的计数函数
fn count_with_cache(
    text: &str,
    counter: &TokenCounter,
    cache: &Cache<String, usize>
) -> usize {
    // 检查缓存
    if let Some(count) = cache.get(text) {
        return count;  // 缓存命中，39 ns
    }
    
    // 缓存未命中，计算并缓存
    let count = counter.count(text);
    cache.put(text.to_string(), count);
    count  // 首次计数，7 µs - 4 ms
}

// 使用
let count1 = count_with_cache("Hello", &counter, &cache);  // 7 µs
let count2 = count_with_cache("Hello", &counter, &cache);  // 39 ns（214x 加速）
```

### 缓存统计

```rust
// 获取缓存统计信息
let stats = cache.stats();
println!("缓存大小: {}/{}", stats.size, stats.capacity);
println!("使用率: {:.2}%", stats.utilization() * 100.0);

// 根据使用率调整
if stats.utilization() > 0.95 {
    // 缓存接近满，考虑扩容或清理
    cache.clear();
}
```

### 缓存容量建议

| 场景 | 推荐容量 | 说明 |
|-----|---------|------|
| 开发/测试 | 1,000 | 够用 |
| 小型生产 | 5,000 | 单用户场景 |
| 中型生产 | 10,000 | 多用户场景 |
| 大型生产 | 50,000+ | 高并发场景 |

---

## 高级用法

### 压缩历史记录

```rust
// 获取压缩历史
for record in service.history() {
    println!("时间: {:?}", record.timestamp);
    println!("策略: {:?}", record.strategy);
    println!("原始消息数: {}", record.original_message_count);
    println!("压缩后消息数: {}", record.compressed_message_count);
    println!("压缩率: {:.2}%", record.compression_ratio * 100.0);
    println!("节省 Token: {}", record.saved_tokens);
    println!("---");
}

// 清空历史
service.clear_history();
```

### 估算 Token 数

在压缩前估算 Token 数，决定是否需要压缩：

```rust
// 估算消息的 Token 数
let estimated_tokens = service.estimate_tokens(&messages)?;
println!("估算 Token 数: {}", estimated_tokens);

// 判断是否需要压缩
if service.should_compress(&messages)? {
    let result = service.auto_compress(&messages).await?;
}
```

### 自定义压缩逻辑

```rust
// 自定义判断逻辑
let token_count = service.estimate_tokens(&messages)?;
let message_count = messages.len();

if token_count > 10000 || message_count > 100 {
    // 使用语义压缩
    let result = service.compress(&messages, StrategyType::Semantic).await?;
} else if message_count > 50 {
    // 使用滑动窗口
    let result = service.compress(&messages, StrategyType::SlidingWindow).await?;
} else {
    // 不压缩
}
```

### 保留重要消息

```rust
// 标记重要消息（系统消息默认保留）
let messages = vec![
    Message { role: "system".to_string(), content: "...".to_string(), /* ... */ },
    Message { role: "user".to_string(), /* ... */ },
    Message { role: "assistant".to_string(), /* ... */ },
];

// 压缩时会自动保留 system 消息
let result = service.compress(&messages, StrategyType::SlidingWindow).await?;
```

---

## 示例代码

### 示例 1: 对话系统集成

```rust
use agent_context_compression::*;

struct ChatBot {
    service: CompressionService,
    history: Vec<Message>,
}

impl ChatBot {
    pub fn new(llm_client: Arc<dyn LLMClient>) -> anyhow::Result<Self> {
        let config = CompressionConfig {
            auto_compress: true,
            token_threshold: 8000,
            default_strategy: StrategyType::Hierarchical,
            ..Default::default()
        };
        
        Ok(Self {
            service: CompressionService::new(llm_client, config)?,
            history: Vec::new(),
        })
    }
    
    pub async fn chat(&mut self, user_message: String) -> anyhow::Result<String> {
        // 添加用户消息
        self.history.push(Message {
            role: "user".to_string(),
            content: user_message,
            /* ... */
        });
        
        // 自动压缩（如果需要）
        let result = self.service.auto_compress(&self.history).await?;
        if result.compressed {
            self.history = result.compressed_messages;
            println!("已压缩: 节省 {} tokens", result.saved_tokens);
        }
        
        // 调用 LLM 生成回复
        let response = self.call_llm(&self.history).await?;
        
        // 添加助手回复
        self.history.push(Message {
            role: "assistant".to_string(),
            content: response.clone(),
            /* ... */
        });
        
        Ok(response)
    }
    
    async fn call_llm(&self, messages: &[Message]) -> anyhow::Result<String> {
        // 调用 LLM API
        // ...
        Ok("...".to_string())
    }
}
```

### 示例 2: 批量处理优化

```rust
use agent_context_compression::{TokenCounter, Cache};

async fn process_messages(messages: Vec<String>) -> anyhow::Result<()> {
    let counter = TokenCounter::new_for_claude()?;
    let cache: Cache<String, usize> = Cache::new(10000);
    
    let mut total_tokens = 0;
    let mut cache_hits = 0;
    let mut cache_misses = 0;
    
    for message in messages {
        let count = if let Some(cached) = cache.get(&message) {
            cache_hits += 1;
            cached
        } else {
            cache_misses += 1;
            let count = counter.count(&message);
            cache.put(message.clone(), count);
            count
        };
        
        total_tokens += count;
    }
    
    println!("总 Token 数: {}", total_tokens);
    println!("缓存命中率: {:.2}%", 
        (cache_hits as f64 / (cache_hits + cache_misses) as f64) * 100.0
    );
    
    Ok(())
}
```

### 示例 3: 配置文件动态加载

```rust
use agent_context_compression::ConfigFile;
use std::path::Path;

fn load_config_from_env() -> anyhow::Result<CompressionConfig> {
    let config_path = std::env::var("COMPRESSION_CONFIG")
        .unwrap_or_else(|_| "compression.toml".to_string());
    
    if Path::new(&config_path).exists() {
        let config_file = ConfigFile::from_file(&config_path)?;
        println!("已加载配置: {}", config_path);
        Ok(config_file.config)
    } else {
        println!("使用默认配置");
        Ok(CompressionConfig::default())
    }
}
```

---

## 常见问题

### 1. 如何选择压缩策略？

- **快速响应优先**: 使用滑动窗口
- **Token 节省优先**: 使用语义压缩
- **不确定场景**: 使用分层压缩（推荐）

### 2. 压缩会丢失信息吗？

- **滑动窗口**: 会丢失旧消息，但保留系统消息
- **语义压缩**: 保留关键信息，但细节可能丢失
- **建议**: 重要对话定期备份原始消息

### 3. 缓存何时失效？

LRU 缓存不会自动失效，只会在容量满时驱逐最久未使用的项。如需定期清理：

```rust
// 定期清空缓存
cache.clear();

// 或手动移除特定项
cache.pop(&"old_key".to_string());
```

### 4. 如何调试压缩问题？

启用详细日志：

```rust
// 查看压缩历史
for record in service.history() {
    println!("{:#?}", record);
}

// 估算 Token 数
let tokens = service.estimate_tokens(&messages)?;
println!("当前 Token 数: {}", tokens);
```

更多问题请查看 [故障排查指南](TROUBLESHOOTING.md)。

---

## 下一步

- 阅读 [最佳实践](BEST_PRACTICES.md) 了解性能优化建议
- 查看 [性能分析报告](../docs/V2_COMPRESSION_PERFORMANCE_ANALYSIS.md) 了解详细性能数据
- 参考 [故障排查指南](TROUBLESHOOTING.md) 解决常见问题

---

**文档版本**: 1.0  
**最后更新**: 2026-04-18
