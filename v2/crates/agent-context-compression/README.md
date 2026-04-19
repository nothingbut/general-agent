# agent-context-compression

[![Tests](https://img.shields.io/badge/tests-63%20passed-brightgreen)]()
[![Coverage](https://img.shields.io/badge/coverage-85%25-brightgreen)]()
[![Rust](https://img.shields.io/badge/rust-1.70%2B-orange)]()

高性能的上下文压缩系统，支持多种压缩策略和 LRU 缓存优化。

## ✨ 特性

- 🚀 **三种压缩策略**
  - 滑动窗口（Sliding Window）：保留最近 N 条消息
  - 语义压缩（Semantic）：LLM 生成摘要
  - 分层压缩（Hierarchical）：智能选择最优策略

- ⚡ **高性能**
  - 滑动窗口：~270 ps（皮秒级）
  - Token 计数：7-670 µs（根据文本长度）
  - LRU 缓存：39 ns 查询，214x 加速

- 🎯 **智能自适应**
  - 根据对话长度和 Token 数自动选择策略
  - 自动触发压缩（可配置阈值）
  - 保留系统消息和重要上下文

- 📦 **易于使用**
  - 简洁的 API 设计
  - 支持 TOML/YAML 配置文件
  - 完整的文档和示例

- ✅ **生产就绪**
  - 63 个单元测试，100% 通过
  - 基准测试覆盖核心场景
  - 线程安全的缓存实现

## 🚀 快速开始

### 基本使用

```rust
use agent_context_compression::{
    CompressionService, CompressionConfig, StrategyType, TokenCounter
};
use agent_llm::anthropic::AnthropicClient;
use std::sync::Arc;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 1. 创建 LLM 客户端
    let llm_client = Arc::new(AnthropicClient::new(
        std::env::var("ANTHROPIC_API_KEY")?
    ));
    
    // 2. 创建压缩服务（使用默认配置）
    let config = CompressionConfig::default();
    let mut service = CompressionService::new(llm_client, config)?;
    
    // 3. 自动压缩（根据阈值自动触发）
    let result = service.auto_compress(&messages).await?;
    
    println!("原始消息数: {}", result.original_count);
    println!("压缩后消息数: {}", result.compressed_count);
    println!("压缩率: {:.2}%", result.compression_ratio * 100.0);
    println!("节省 Token: {}", result.saved_tokens);
    
    Ok(())
}
```

### 手动选择策略

```rust
// 使用滑动窗口策略
let result = service.compress(&messages, StrategyType::SlidingWindow).await?;

// 使用语义压缩策略
let result = service.compress(&messages, StrategyType::Semantic).await?;

// 使用分层压缩策略（推荐）
let result = service.compress(&messages, StrategyType::Hierarchical).await?;
```

### 使用配置文件

```rust
use agent_context_compression::ConfigFile;

// 从 TOML 文件加载配置
let config = ConfigFile::from_file("compression.toml")?;

// 创建服务
let service = CompressionService::new(llm_client, config.config)?;
```

**compression.toml** 示例：
```toml
auto_compress = true
token_threshold = 8000
compression_history_limit = 10
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

### 使用 LRU 缓存优化

```rust
use agent_context_compression::{TokenCounter, Cache};

// 创建 Token 计数器
let counter = TokenCounter::new_for_claude()?;

// 创建缓存（容量 1000）
let cache: Cache<String, usize> = Cache::new(1000);

// 带缓存的 Token 计数
fn count_with_cache(text: &str, counter: &TokenCounter, cache: &Cache<String, usize>) -> usize {
    // 检查缓存
    if let Some(count) = cache.get(text) {
        return count; // 缓存命中，214x 加速
    }
    
    // 缓存未命中，计算并缓存
    let count = counter.count(text);
    cache.put(text.to_string(), count);
    count
}
```

## 📦 安装

在 `Cargo.toml` 中添加依赖：

```toml
[dependencies]
agent-context-compression = { path = "../agent-context-compression" }
```

## 📖 文档

- **[使用指南](docs/USAGE.md)** - 详细的使用说明
- **[最佳实践](docs/BEST_PRACTICES.md)** - 性能调优和最佳实践
- **[故障排查](docs/TROUBLESHOOTING.md)** - 常见问题和解决方案
- **[性能分析](docs/V2_COMPRESSION_PERFORMANCE_ANALYSIS.md)** - 详细的性能基准测试报告
- **[API 文档](https://docs.rs/agent-context-compression)** - 完整的 API 参考

## 🏗️ 架构

```
agent-context-compression/
├── src/
│   ├── lib.rs                  # 公共接口
│   ├── error.rs                # 错误类型
│   ├── models.rs               # 数据模型
│   ├── cache.rs                # LRU 缓存（Day 7）
│   ├── config.rs               # 配置文件支持（Day 6）
│   ├── token_counter.rs        # Token 计数器（Day 1）
│   ├── service.rs              # 压缩服务（Day 5）
│   └── strategies/
│       ├── mod.rs              # 策略接口
│       ├── sliding_window.rs   # 滑动窗口策略（Day 2）
│       ├── semantic.rs         # 语义压缩策略（Day 3）
│       └── hierarchical.rs     # 分层压缩策略（Day 4）
└── benches/
    └── compression_benchmarks.rs  # 基准测试（Day 7）
```

## ⚡ 性能

### Token 计数性能

| 文本长度 | 耗时 | 吞吐量 |
|---------|------|--------|
| 10 词   | 7.04 µs | ~142k ops/s |
| 100 词  | 67.02 µs | ~14.9k ops/s |
| 1000 词 | 670.1 µs | ~1.5k ops/s |
| 5000 词 | 4.60 ms | ~217 ops/s |

### LRU 缓存性能

| 操作 | 耗时 | 说明 |
|-----|------|------|
| 插入（put） | 80 ns | 极快 |
| 查询命中（get_hit） | 39 ns | 极快 |
| 查询未命中（get_miss） | 87 ns | 极快 |

### 缓存收益

| 场景 | 无缓存 | 有缓存 | 提升 |
|-----|--------|--------|------|
| 100 条消息（10% 重复） | 197.20 µs | 920 ns | **214x** 🚀 |

详细性能报告请查看 [V2_COMPRESSION_PERFORMANCE_ANALYSIS.md](docs/V2_COMPRESSION_PERFORMANCE_ANALYSIS.md)。

## 🧪 测试

```bash
# 运行所有单元测试
cargo test --package agent-context-compression

# 运行基准测试
cargo bench --package agent-context-compression

# 查看测试覆盖率
cargo tarpaulin --package agent-context-compression
```

测试统计:
- **总测试数**: 63 个
- **通过率**: 100%
- **覆盖率**: 85%+

## 🎯 使用场景

### 1. 对话系统

保留重要上下文的同时控制 Token 使用：

```rust
// 配置自动压缩阈值
let mut config = CompressionConfig::default();
config.token_threshold = 8000;  // 超过 8000 tokens 自动压缩

let mut service = CompressionService::new(llm_client, config)?;

// 每次对话后自动检查是否需要压缩
let result = service.auto_compress(&conversation_history).await?;
```

### 2. 长对话归档

定期压缩历史对话节省存储空间：

```rust
// 使用语义压缩生成摘要
let result = service.compress(&old_messages, StrategyType::Semantic).await?;

// 保存摘要，删除原始消息
archive_summary(result.compressed_messages);
```

### 3. 批量消息处理

使用缓存加速重复内容的处理：

```rust
let cache: Cache<String, usize> = Cache::new(10000);

for message in messages {
    let token_count = if let Some(count) = cache.get(&message.content) {
        count  // 缓存命中，214x 加速
    } else {
        let count = counter.count(&message.content);
        cache.put(message.content.clone(), count);
        count
    };
    
    // 处理消息...
}
```

## 🔧 配置

### 压缩策略对比

| 策略 | 压缩速度 | 压缩率 | Token 节省 | 适用场景 |
|-----|---------|--------|-----------|---------|
| 滑动窗口 | 极快（~270 ps） | 低 | 少 | 短对话、快速响应 |
| 语义压缩 | 慢（需 LLM） | 高 | 多 | 长对话、归档 |
| 分层压缩 | 自适应 | 中-高 | 中-多 | 通用（推荐）|

### 推荐配置

**开发环境**:
```toml
auto_compress = false  # 手动触发，便于调试
token_threshold = 4000
default_strategy = "sliding_window"  # 快速
```

**生产环境**:
```toml
auto_compress = true
token_threshold = 8000
default_strategy = "hierarchical"  # 智能选择
compression_history_limit = 50
```

**高并发场景**:
- 使用 LRU 缓存（容量 10000+）
- 考虑分片缓存优化锁竞争

## 📊 监控和调优

### 缓存使用率监控

```rust
let stats = cache.stats();
println!("缓存大小: {}/{}", stats.size, stats.capacity);
println!("使用率: {:.2}%", stats.utilization() * 100.0);

// 如果使用率 > 95%，考虑扩容
if stats.utilization() > 0.95 {
    // 创建更大的缓存
    let larger_cache: Cache<String, usize> = Cache::new(stats.capacity * 2);
}
```

### 压缩历史分析

```rust
for record in service.history() {
    println!(
        "策略: {:?} | 压缩率: {:.2}% | 节省: {} tokens | 耗时: {}ms",
        record.strategy,
        record.compression_ratio * 100.0,
        record.saved_tokens,
        record.duration_ms
    );
}
```

## 🤝 贡献

欢迎贡献代码、报告问题或提出建议！

## 📄 许可证

MIT License

## 🔗 相关项目

- [agent-core](../agent-core) - 核心数据模型
- [agent-llm](../agent-llm) - LLM 客户端集成

## 📝 变更日志

### Week 2 (2026-04-16 ~ 2026-04-20)

- **Day 6**: ✅ 配置文件支持（TOML/YAML）
- **Day 7**: ✅ LRU 缓存优化 + 基准测试
- **Day 8**: ✅ 文档完善 + 最终验收

### Week 1 (2026-04-15 ~ 2026-04-16)

- **Day 1**: ✅ Token 计数器
- **Day 2**: ✅ 滑动窗口策略
- **Day 3**: ✅ 语义压缩策略
- **Day 4**: ✅ 分层压缩策略
- **Day 5**: ✅ 压缩服务集成

详细变更记录请查看 [完成报告文档](docs/)。

---

**开发团队**: General Agent V2 Team  
**最后更新**: 2026-04-18  
**版本**: 0.1.0
