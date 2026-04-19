# 上下文压缩系统 Week 2 实施计划

**功能**: 上下文压缩系统扩展与集成
**优先级**: ⭐⭐⭐⭐ (P1)
**预计耗时**: 3 个工作日
**开始日期**: 2026-04-18
**结束日期**: 2026-04-20
**状态**: 📋 进行中

---

## 🎯 目标

Week 1 已完成核心压缩功能（51 个测试全部通过）。Week 2 专注于：
1. **实用功能扩展**：配置文件、性能测试、缓存优化
2. **系统集成**：与现有系统集成，提供完整的使用体验
3. **生产就绪**：文档、示例、最佳实践

---

## 📋 Week 1 完成情况回顾

### ✅ 已完成
- Token 计数器（10 测试）
- 滑动窗口策略（11 测试）
- 语义压缩策略（8 测试）
- 分层压缩策略（10 测试）
- 压缩服务（12 测试）

### 📊 统计
- **总测试数**: 51 个（100% 通过）
- **代码行数**: ~1900 行
- **测试覆盖率**: 100%

---

## 📅 Week 2 实施计划

### Day 6 (2026-04-18): 配置系统与示例

#### 任务清单
- [ ] 实现配置文件支持（TOML/YAML）
- [ ] 创建示例程序（examples/）
- [ ] 使用文档（README.md）
- [ ] 最佳实践指南
- [ ] 5-8 个配置相关测试

#### 技术细节

**Cargo.toml 新增依赖**
```toml
[dependencies]
# 配置文件支持
config = "0.14"
toml = "0.8"
serde_yaml = "0.9"
```

**src/config.rs**
```rust
use crate::models::CompressionConfig;
use serde::{Deserialize, Serialize};
use std::path::Path;

/// 配置文件格式
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ConfigFile {
    pub compression: CompressionConfig,
    
    #[serde(default)]
    pub strategies: StrategyConfigs,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct StrategyConfigs {
    pub sliding_window: SlidingWindowConfig,
    pub semantic: SemanticConfig,
    pub hierarchical: HierarchicalConfig,
}

impl Default for StrategyConfigs {
    fn default() -> Self {
        Self {
            sliding_window: SlidingWindowConfig {
                window_size: 10,
            },
            semantic: SemanticConfig {
                target_tokens: 2000,
                model: "claude-3-5-sonnet-20241022".to_string(),
            },
            hierarchical: HierarchicalConfig {
                small_threshold: 20,
                large_threshold: 50,
                large_token_threshold: 8000,
            },
        }
    }
}

impl ConfigFile {
    /// 从文件加载配置
    pub fn from_file<P: AsRef<Path>>(path: P) -> Result<Self> {
        let content = std::fs::read_to_string(path)?;
        let ext = path.as_ref().extension()
            .and_then(|s| s.to_str())
            .unwrap_or("toml");
        
        match ext {
            "toml" => Ok(toml::from_str(&content)?),
            "yaml" | "yml" => Ok(serde_yaml::from_str(&content)?),
            _ => Err(crate::CompressionError::InvalidConfig(
                format!("Unsupported config file format: {}", ext)
            ).into()),
        }
    }
    
    /// 保存配置到文件
    pub fn save_to_file<P: AsRef<Path>>(&self, path: P) -> Result<()> {
        let ext = path.as_ref().extension()
            .and_then(|s| s.to_str())
            .unwrap_or("toml");
        
        let content = match ext {
            "toml" => toml::to_string_pretty(self)?,
            "yaml" | "yml" => serde_yaml::to_string(self)?,
            _ => return Err(crate::CompressionError::InvalidConfig(
                format!("Unsupported config file format: {}", ext)
            ).into()),
        };
        
        std::fs::write(path, content)?;
        Ok(())
    }
}
```

**compression.toml 示例**
```toml
[compression]
auto_trigger_threshold = 15
sliding_window_size = 10
semantic_target_tokens = 2000
auto_compression_enabled = true

[strategies.sliding_window]
window_size = 10

[strategies.semantic]
target_tokens = 2000
model = "claude-3-5-sonnet-20241022"

[strategies.hierarchical]
small_threshold = 20
large_threshold = 50
large_token_threshold = 8000
```

**examples/basic_usage.rs**
```rust
//! 基本使用示例

use agent_context_compression::{
    CompressionService, CompressionConfig, StrategyType
};
use agent_llm::anthropic::AnthropicClient;
use std::sync::Arc;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 1. 创建 LLM 客户端
    let llm_client = Arc::new(AnthropicClient::new(
        std::env::var("ANTHROPIC_API_KEY")?
    ));
    
    // 2. 加载配置
    let config = CompressionConfig::default();
    
    // 3. 创建压缩服务
    let mut service = CompressionService::new(llm_client, config)?;
    
    // 4. 创建一些测试消息
    let messages = create_test_messages(20);
    
    // 5. 自动压缩
    println!("原始消息数: {}", messages.len());
    let result = service.auto_compress(&messages).await?;
    println!("压缩后消息数: {}", result.compressed_count);
    println!("压缩率: {:.2}%", result.compression_ratio * 100.0);
    
    // 6. 查看压缩历史
    for record in service.history() {
        println!("压缩记录: {} -> {} ({}%)", 
            record.original_message_count,
            record.compressed_message_count,
            (record.compression_ratio * 100.0) as u32
        );
    }
    
    Ok(())
}

fn create_test_messages(count: usize) -> Vec<agent_core::models::Message> {
    // ... 创建测试消息
}
```

**examples/with_config_file.rs**
```rust
//! 使用配置文件的示例

use agent_context_compression::ConfigFile;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 从文件加载配置
    let config = ConfigFile::from_file("compression.toml")?;
    
    // 使用配置创建服务
    let service = create_service_from_config(config)?;
    
    // ... 使用服务
    
    Ok(())
}
```

#### 验收标准
- ✅ 支持 TOML 和 YAML 配置文件
- ✅ 提供 2-3 个完整的示例程序
- ✅ README.md 包含使用说明
- ✅ 配置相关测试通过

---

### Day 7 (2026-04-19): 性能优化与基准测试

#### 任务清单
- [ ] 性能基准测试（Criterion）
- [ ] 缓存优化（LRU Cache）
- [ ] 并发压缩支持
- [ ] 性能分析报告
- [ ] 3-5 个性能测试

#### 技术细节

**Cargo.toml 新增依赖**
```toml
[dependencies]
# 缓存
lru = "0.12"

[dev-dependencies]
# 性能测试
criterion = { version = "0.5", features = ["async_tokio"] }

[[bench]]
name = "compression_benchmarks"
harness = false
```

**benches/compression_benchmarks.rs**
```rust
use criterion::{black_box, criterion_group, criterion_main, Criterion, BenchmarkId};
use agent_context_compression::*;
use tokio::runtime::Runtime;

fn benchmark_token_counting(c: &mut Criterion) {
    let counter = TokenCounter::new_for_claude().unwrap();
    let text = "Hello, world! This is a test message.".repeat(10);
    
    c.bench_function("token_count_short", |b| {
        b.iter(|| counter.count(black_box(&text)))
    });
}

fn benchmark_sliding_window(c: &mut Criterion) {
    let rt = Runtime::new().unwrap();
    let strategy = SlidingWindowStrategy::new(10).unwrap();
    
    let mut group = c.benchmark_group("sliding_window");
    
    for size in [10, 50, 100, 500].iter() {
        let messages = create_test_messages(*size);
        
        group.bench_with_input(
            BenchmarkId::from_parameter(size),
            size,
            |b, _| {
                b.to_async(&rt).iter(|| async {
                    strategy.compress(black_box(&messages)).await.unwrap()
                })
            },
        );
    }
    
    group.finish();
}

fn benchmark_semantic_compression(c: &mut Criterion) {
    // Mock LLM 客户端进行基准测试
    // ...
}

criterion_group!(
    benches,
    benchmark_token_counting,
    benchmark_sliding_window,
    benchmark_semantic_compression
);
criterion_main!(benches);
```

**src/cache.rs**
```rust
use lru::LruCache;
use std::num::NonZeroUsize;
use std::sync::Mutex;

/// 压缩结果缓存
pub struct CompressionCache {
    cache: Mutex<LruCache<String, Vec<agent_core::models::Message>>>,
}

impl CompressionCache {
    pub fn new(capacity: usize) -> Self {
        Self {
            cache: Mutex::new(LruCache::new(
                NonZeroUsize::new(capacity).unwrap()
            )),
        }
    }
    
    pub fn get(&self, key: &str) -> Option<Vec<agent_core::models::Message>> {
        self.cache.lock().unwrap().get(key).cloned()
    }
    
    pub fn put(&self, key: String, value: Vec<agent_core::models::Message>) {
        self.cache.lock().unwrap().put(key, value);
    }
    
    /// 生成缓存键
    pub fn cache_key(messages: &[agent_core::models::Message], strategy: &str) -> String {
        use std::collections::hash_map::DefaultHasher;
        use std::hash::{Hash, Hasher};
        
        let mut hasher = DefaultHasher::new();
        messages.len().hash(&mut hasher);
        strategy.hash(&mut hasher);
        
        // 哈希前几条和后几条消息的内容
        if messages.len() > 0 {
            messages[0].content.hash(&mut hasher);
        }
        if messages.len() > 1 {
            messages[messages.len() - 1].content.hash(&mut hasher);
        }
        
        format!("{:x}", hasher.finish())
    }
}
```

**性能报告模板**
```markdown
# 性能基准测试报告

## 测试环境
- CPU: [处理器型号]
- 内存: [内存大小]
- Rust 版本: 1.75.0
- 测试时间: 2026-04-19

## 测试结果

### Token 计数性能
| 文本长度 | 平均耗时 | 吞吐量 |
|---------|---------|--------|
| 100字符 | 0.5μs | 2M ops/s |
| 1000字符 | 2.5μs | 400K ops/s |
| 10000字符 | 25μs | 40K ops/s |

### 滑动窗口压缩性能
| 消息数 | 平均耗时 | 吞吐量 |
|-------|---------|--------|
| 10条 | 5μs | 200K ops/s |
| 50条 | 20μs | 50K ops/s |
| 100条 | 35μs | 28K ops/s |
| 500条 | 180μs | 5.5K ops/s |

### 语义压缩性能
| 消息数 | 平均耗时 | 备注 |
|-------|---------|------|
| 10条 | 2.1s | 含 LLM 调用 |
| 50条 | 3.5s | 含 LLM 调用 |
| 100条 | 5.2s | 含 LLM 调用 |

## 优化建议
1. ✅ Token 计数：性能优秀，无需优化
2. ✅ 滑动窗口：性能优秀，无需优化
3. ⚠️ 语义压缩：考虑添加缓存，减少重复 LLM 调用
```

#### 验收标准
- ✅ 完成 Criterion 基准测试
- ✅ 实现 LRU 缓存
- ✅ 性能报告完整
- ✅ 性能满足指标（滑动窗口 < 10ms）

---

### Day 8 (2026-04-20): 文档与最终验收

#### 任务清单
- [ ] 完善 API 文档（Rust Doc）
- [ ] 使用指南（docs/）
- [ ] 故障排查指南
- [ ] 最佳实践文档
- [ ] Week 2 完成报告

#### 文档清单

**README.md**
```markdown
# agent-context-compression

高性能的上下文压缩系统，支持多种压缩策略。

## 特性

- 🚀 **三种压缩策略**：滑动窗口、语义压缩、分层压缩
- ⚡ **高性能**：滑动窗口 < 10ms，Token 计数 < 1ms
- 🎯 **智能自适应**：根据对话特征自动选择最优策略
- 📦 **易于使用**：简洁的 API，完整的文档
- ✅ **生产就绪**：51 个测试，100% 覆盖率

## 快速开始

```rust
use agent_context_compression::*;

// 创建服务
let config = CompressionConfig::default();
let mut service = CompressionService::new(llm_client, config)?;

// 自动压缩
let result = service.auto_compress(&messages).await?;
println!("压缩率: {:.2}%", result.compression_ratio * 100.0);
```

## 安装

```toml
[dependencies]
agent-context-compression = { path = "../agent-context-compression" }
```

## 文档

- [使用指南](docs/USAGE.md)
- [API 文档](https://docs.rs/agent-context-compression)
- [示例程序](examples/)
- [性能报告](docs/PERFORMANCE.md)
- [故障排查](docs/TROUBLESHOOTING.md)

## 性能

| 操作 | 性能 |
|------|------|
| Token 计数 | < 1ms |
| 滑动窗口压缩 | < 10ms |
| 语义压缩 | 2-5s (LLM) |

## 测试

```bash
cargo test --package agent-context-compression
```

## 许可

MIT License
```

**docs/USAGE.md** - 完整使用指南
**docs/PERFORMANCE.md** - 性能基准测试报告
**docs/TROUBLESHOOTING.md** - 故障排查指南
**docs/BEST_PRACTICES.md** - 最佳实践

#### 验收标准
- ✅ README.md 完整清晰
- ✅ API 文档 100% 覆盖
- ✅ 使用指南详细
- ✅ 故障排查指南实用

---

## ✅ Week 2 验收标准

### 功能验收
- ✅ 配置文件支持（TOML/YAML）
- ✅ 示例程序完整（2-3 个）
- ✅ 性能基准测试完成
- ✅ LRU 缓存实现
- ✅ 文档完善

### 性能验收
- ✅ 滑动窗口: < 10ms
- ✅ Token 计数: < 1ms
- ✅ 缓存命中率: > 80% (重复场景)

### 质量验收
- ✅ 所有测试通过 (55+ 个)
- ✅ 文档覆盖率 100%
- ✅ 示例可运行
- ✅ 性能报告完整

---

## 🎉 Week 1 + Week 2 总体成就

### 完成内容
- ✅ **核心功能** (Week 1): 3 种压缩策略，51 个测试
- ✅ **实用功能** (Week 2): 配置文件、性能测试、缓存
- ✅ **文档完善** (Week 2): 使用指南、API 文档、示例
- ✅ **生产就绪**: 性能优异，文档完整，测试充分

### 代码统计
```
总代码行数: ~2500 行
├── 核心代码: ~1100 行 (Week 1: 900, Week 2: 200)
├── 测试代码: ~1000 行 (Week 1: 800, Week 2: 200)
├── 文档代码: ~400 行
└── 总测试数: 55+ 个
```

---

## 🚀 后续扩展（可选）

如果需要进一步扩展：
- [ ] 数据库持久化（压缩历史）
- [ ] Web API 接口
- [ ] 可视化工具（压缩率统计图表）
- [ ] 更多压缩策略（基于规则、基于关键词）
- [ ] 分布式压缩支持

---

**最后更新**: 2026-04-18
**版本**: Context Compression Week 2 Plan v1.0
**维护者**: General Agent Team
