# 故障排查

本文档提供 `agent-context-compression` 常见问题的诊断和解决方案。

## 目录

- [常见问题](#常见问题)
- [性能问题](#性能问题)
- [缓存问题](#缓存问题)
- [配置错误](#配置错误)
- [测试失败](#测试失败)
- [生产环境问题](#生产环境问题)
- [诊断工具](#诊断工具)

---

## 常见问题

### Q1: 压缩后消息数量没有减少

**症状**:
```rust
let result = service.compress(&messages, StrategyType::SlidingWindow).await?;
println!("原始: {}, 压缩后: {}", result.original_count, result.compressed_count);
// 输出: 原始: 10, 压缩后: 10（没有变化）
```

**可能原因**:

1. **消息数量低于阈值**

```rust
// 检查配置
let config = service.config();
println!("滑动窗口大小: {}", config.sliding_window_size);

// 如果 window_size = 20，消息只有 10 条，不会压缩
```

**解决方案**: 调整窗口大小或使用其他策略

```rust
let mut config = CompressionConfig::default();
config.sliding_window_size = 5;  // 降低阈值
```

2. **系统消息占比高**

```rust
// 检查消息角色分布
let system_count = messages.iter().filter(|m| m.role == "system").count();
println!("系统消息: {}/{}", system_count, messages.len());

// 如果系统消息占大多数，且 preserve_system = true，压缩效果有限
```

**解决方案**: 考虑不保留系统消息（如果允许）

```rust
config.preserve_system_messages = false;
```

### Q2: 自动压缩不触发

**症状**:
```rust
let result = service.auto_compress(&messages).await?;
println!("是否压缩: {}", result.compressed);
// 输出: 是否压缩: false
```

**诊断步骤**:

1. **检查是否启用自动压缩**

```rust
let config = service.config();
println!("自动压缩: {}", config.auto_compress);
```

2. **检查 Token 阈值**

```rust
let estimated_tokens = service.estimate_tokens(&messages)?;
println!("当前 Token 数: {}", estimated_tokens);
println!("阈值: {}", config.token_threshold);

// 如果 estimated_tokens < token_threshold，不会触发
```

3. **检查是否需要压缩**

```rust
let should_compress = service.should_compress(&messages)?;
println!("是否需要压缩: {}", should_compress);
```

**解决方案**:

```rust
// 降低阈值
let mut config = CompressionConfig::default();
config.auto_compress = true;
config.token_threshold = 4000;  // 降低到 4000
```

### Q3: 语义压缩失败

**症状**:
```rust
let result = service.compress(&messages, StrategyType::Semantic).await;
// Error: LLM API 调用失败
```

**可能原因**:

1. **API 密钥无效**

```rust
// 检查 API 密钥
let api_key = std::env::var("ANTHROPIC_API_KEY")
    .expect("ANTHROPIC_API_KEY not set");
println!("API Key 前缀: {}", &api_key[..10]);
```

**解决方案**:
```bash
export ANTHROPIC_API_KEY="sk-ant-your-key-here"
```

2. **网络连接问题**

```bash
# 测试 API 连接
curl https://api.anthropic.com/v1/messages \
  -H "x-api-key: $ANTHROPIC_API_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -d '{
    "model": "claude-3-5-sonnet-20241022",
    "max_tokens": 10,
    "messages": [{"role": "user", "content": "Hi"}]
  }'
```

3. **超时**

```rust
// 增加超时时间
let client = AnthropicClient::builder()
    .timeout(Duration::from_secs(60))  // 60 秒超时
    .build(api_key);
```

4. **消息格式错误**

```rust
// 验证消息格式
for message in &messages {
    if message.role.is_empty() || message.content.is_empty() {
        eprintln!("无效消息: {:?}", message);
    }
}
```

### Q4: Token 计数不准确

**症状**:
```rust
let count1 = counter.count("Hello, world!");
println!("计数: {}", count1);  // 输出: 5
// 预期: 4
```

**可能原因**:

1. **模型选择错误**

```rust
// 检查使用的模型
let counter = TokenCounter::new_for_model("gpt-4")?;  // 错误！
// 应该使用
let counter = TokenCounter::new_for_claude()?;
```

2. **计数器未正确初始化**

```rust
// 重新创建计数器
let counter = TokenCounter::new_for_claude()
    .map_err(|e| {
        eprintln!("Token 计数器初始化失败: {}", e);
        e
    })?;
```

**解决方案**:

```rust
// 验证计数器
let test_text = "Hello";
let count = counter.count(test_text);
assert!(count > 0, "Token 计数器异常");
```

---

## 性能问题

### 问题 1: 压缩速度慢

**症状**: 压缩操作耗时 > 5 秒

**诊断**:

```rust
use std::time::Instant;

let start = Instant::now();
let result = service.compress(&messages, StrategyType::Hierarchical).await?;
let duration = start.elapsed();

println!("压缩耗时: {:?}", duration);
println!("策略: {:?}", result.strategy);
println!("消息数: {}", messages.len());
```

**可能原因和解决方案**:

1. **使用了语义压缩**

```rust
// 检查实际使用的策略
println!("实际策略: {:?}", result.strategy);

// 如果是 Semantic，考虑切换到滑动窗口
let config = CompressionConfig {
    default_strategy: StrategyType::SlidingWindow,
    ..Default::default()
};
```

2. **消息数量过多**

```rust
if messages.len() > 100 {
    println!("警告: 消息数量过多 ({})", messages.len());
    
    // 分批压缩
    let chunk_size = 50;
    for chunk in messages.chunks(chunk_size) {
        let result = service.compress(chunk, StrategyType::SlidingWindow).await?;
    }
}
```

3. **Token 计数缓慢**

```rust
// 使用缓存加速
let cache: Cache<String, usize> = Cache::new(10_000);

fn count_with_cache(
    text: &str,
    counter: &TokenCounter,
    cache: &Cache<String, usize>
) -> usize {
    cache.get(text).unwrap_or_else(|| {
        let count = counter.count(text);
        cache.put(text.to_string(), count);
        count
    })
}
```

### 问题 2: 内存使用过高

**症状**: 应用内存占用持续增长

**诊断**:

```bash
# Linux
ps aux | grep your-app

# macOS
top -pid $(pgrep your-app)

# 或使用 heaptrack
heaptrack ./your-app
heaptrack_gui heaptrack.your-app.*.gz
```

**可能原因**:

1. **缓存容量过大**

```rust
// 检查缓存使用情况
let stats = cache.stats();
println!("缓存大小: {}/{}", stats.size, stats.capacity);
println!("内存占用估算: {} MB", 
    (stats.capacity * 104) / 1_024 / 1_024);

// 如果缓存过大，降低容量
let smaller_cache = Cache::new(5_000);  // 从 50K 降到 5K
```

2. **压缩历史记录过多**

```rust
// 检查历史记录数量
let history_count = service.history().len();
println!("历史记录数: {}", history_count);

// 定期清理
if history_count > 100 {
    service.clear_history();
}
```

3. **消息未释放**

```rust
// 显式释放旧消息
let mut messages = vec![/* ... */];
let result = service.compress(&messages, StrategyType::SlidingWindow).await?;

// 替换旧消息
messages = result.compressed_messages;
```

### 问题 3: CPU 使用率高

**症状**: CPU 使用率 > 80%

**诊断**:

```bash
# 使用 flamegraph 分析
cargo install flamegraph
cargo flamegraph --bin your-app

# 或使用 perf (Linux)
perf record -g ./your-app
perf report
```

**可能原因**:

1. **频繁的 Token 计数**

```rust
// 添加缓存
let cache = Cache::new(10_000);

// 批量计数时复用计数器
let counter = TokenCounter::new_for_claude()?;
for message in messages {
    let count = count_with_cache(&message.content, &counter, &cache);
}
```

2. **锁竞争**

```rust
// 使用分片缓存降低竞争
let sharded_cache = ShardedCache::new(
    50_000,  // 总容量
    16       // 16 个分片
);
```

---

## 缓存问题

### 问题 1: 缓存命中率低

**症状**: 缓存命中率 < 30%

**诊断**:

```rust
struct CacheHitTracker {
    hits: AtomicUsize,
    misses: AtomicUsize,
}

impl CacheHitTracker {
    pub fn hit(&self) {
        self.hits.fetch_add(1, Ordering::Relaxed);
    }
    
    pub fn miss(&self) {
        self.misses.fetch_add(1, Ordering::Relaxed);
    }
    
    pub fn hit_rate(&self) -> f64 {
        let hits = self.hits.load(Ordering::Relaxed);
        let misses = self.misses.load(Ordering::Relaxed);
        let total = hits + misses;
        
        if total == 0 {
            return 0.0;
        }
        
        hits as f64 / total as f64
    }
}

// 使用
let tracker = Arc::new(CacheHitTracker::default());

fn count_with_tracking(
    text: &str,
    counter: &TokenCounter,
    cache: &Cache<String, usize>,
    tracker: &CacheHitTracker
) -> usize {
    if let Some(count) = cache.get(text) {
        tracker.hit();
        return count;
    }
    
    tracker.miss();
    let count = counter.count(text);
    cache.put(text.to_string(), count);
    count
}

// 定期报告
println!("缓存命中率: {:.2}%", tracker.hit_rate() * 100.0);
```

**可能原因和解决方案**:

1. **缓存容量太小**

```rust
// 增加容量
let larger_cache = Cache::new(50_000);  // 从 1K 增加到 50K
```

2. **内容重复度低**

```rust
// 分析内容重复度
let mut content_map: HashMap<String, usize> = HashMap::new();
for message in &messages {
    *content_map.entry(message.content.clone()).or_insert(0) += 1;
}

let duplicates = content_map.values().filter(|&&count| count > 1).count();
let duplicate_rate = duplicates as f64 / content_map.len() as f64;

println!("内容重复率: {:.2}%", duplicate_rate * 100.0);

// 如果重复率 < 10%，缓存效果有限
if duplicate_rate < 0.1 {
    println!("建议: 内容重复率低，缓存收益有限");
}
```

3. **缓存过期策略不当**

```rust
// 实现 TTL 缓存
struct TtlCache {
    cache: Cache<String, (usize, Instant)>,
    ttl: Duration,
}

impl TtlCache {
    pub fn get(&self, key: &str) -> Option<usize> {
        self.cache.get(key).and_then(|(value, timestamp)| {
            if timestamp.elapsed() < self.ttl {
                Some(value)
            } else {
                self.cache.pop(&key.to_string());
                None
            }
        })
    }
}
```

### 问题 2: 缓存使用率过高

**症状**: 缓存使用率持续 > 95%

**诊断**:

```rust
let stats = cache.stats();
println!("缓存使用率: {:.2}%", stats.utilization() * 100.0);

if stats.utilization() > 0.95 {
    println!("警告: 缓存接近满载");
}
```

**解决方案**:

1. **扩容**

```rust
// 创建更大的缓存
let new_capacity = stats.capacity * 2;
let larger_cache = Cache::new(new_capacity);

// 迁移数据（如果需要）
```

2. **定期清理**

```rust
use tokio::time::{interval, Duration};

async fn periodic_cache_cleanup(cache: Arc<Cache<String, usize>>) {
    let mut interval = interval(Duration::from_secs(3600));  // 每小时
    
    loop {
        interval.tick().await;
        
        let stats = cache.stats();
        if stats.utilization() > 0.90 {
            cache.clear();
            println!("缓存已清理");
        }
    }
}
```

---

## 配置错误

### 问题 1: 配置文件加载失败

**症状**:
```rust
let config = ConfigFile::from_file("compression.toml");
// Error: 无法解析配置文件
```

**诊断**:

```rust
use std::fs;

// 检查文件是否存在
if !std::path::Path::new("compression.toml").exists() {
    eprintln!("配置文件不存在");
}

// 读取文件内容
let content = fs::read_to_string("compression.toml")?;
println!("配置文件内容:\n{}", content);

// 尝试手动解析
let config: CompressionConfig = toml::from_str(&content)
    .map_err(|e| {
        eprintln!("TOML 解析错误: {}", e);
        e
    })?;
```

**常见 TOML 错误**:

1. **缩进错误**

```toml
# 错误
[strategies.sliding_window]
  window_size = 20  # 不要缩进

# 正确
[strategies.sliding_window]
window_size = 20
```

2. **引号错误**

```toml
# 错误
default_strategy = hierarchical  # 字符串需要引号

# 正确
default_strategy = "hierarchical"
```

3. **类型错误**

```toml
# 错误
token_threshold = "8000"  # 数字不要加引号

# 正确
token_threshold = 8000
```

**验证配置**:

```rust
fn validate_config_file(path: &str) -> anyhow::Result<()> {
    let config_file = ConfigFile::from_file(path)?;
    let config = config_file.config;
    
    // 验证范围
    assert!(config.token_threshold >= 1000, "token_threshold 太小");
    assert!(config.token_threshold <= 100_000, "token_threshold 太大");
    assert!(config.sliding_window_size >= 5, "window_size 太小");
    assert!(config.sliding_window_size <= 200, "window_size 太大");
    
    println!("✓ 配置文件验证通过");
    Ok(())
}
```

### 问题 2: 环境变量未生效

**症状**: 环境变量设置了但未生效

**诊断**:

```rust
// 列出所有环境变量
for (key, value) in std::env::vars() {
    if key.starts_with("ANTHROPIC") || key.starts_with("COMPRESSION") {
        println!("{} = {}", key, value);
    }
}

// 检查特定变量
match std::env::var("ANTHROPIC_API_KEY") {
    Ok(val) => println!("API Key: {}...", &val[..10]),
    Err(e) => eprintln!("API Key 未设置: {}", e),
}
```

**解决方案**:

```bash
# .env 文件
ANTHROPIC_API_KEY=sk-ant-xxx

# 加载 .env
cargo install dotenvy-cli
dotenvy -f .env -- cargo run
```

```rust
// 代码中加载 .env
use dotenvy::dotenv;

fn main() -> anyhow::Result<()> {
    dotenv().ok();  // 加载 .env 文件
    
    let api_key = std::env::var("ANTHROPIC_API_KEY")?;
    // ...
}
```

---

## 测试失败

### 问题 1: Token 计数测试失败

**症状**:
```
test token_counter::tests::test_count_tokens ... FAILED
assertion failed: count == 4
```

**诊断**:

```rust
#[test]
fn debug_token_count() {
    let counter = TokenCounter::new_for_claude().unwrap();
    
    let test_cases = vec![
        ("Hello", 1),
        ("Hello, world!", 4),
        ("This is a test.", 5),
    ];
    
    for (text, expected) in test_cases {
        let actual = counter.count(text);
        println!("{:?} -> actual: {}, expected: {}", text, actual, expected);
        
        if actual != expected {
            println!("  差异: {} tokens", (actual as i32 - expected as i32).abs());
        }
    }
}
```

**可能原因**:

1. **tiktoken 版本不同**

```toml
# 检查 Cargo.toml
[dependencies]
tiktoken-rs = "0.5"  # 确保版本一致
```

2. **编码器不匹配**

```rust
// 确认使用正确的编码器
let counter = TokenCounter::new_for_claude()?;
println!("编码器: cl100k_base");  // Claude 使用的编码器
```

### 问题 2: 异步测试失败

**症状**:
```
test compression::tests::test_semantic_compression ... FAILED
thread 'compression::tests::test_semantic_compression' panicked at 'there is no reactor running'
```

**解决方案**:

```rust
// 使用 tokio::test
#[tokio::test]
async fn test_semantic_compression() {
    let llm_client = Arc::new(MockLLMClient::new());
    let service = CompressionService::new(llm_client, CompressionConfig::default()).unwrap();
    
    let messages = vec![/* ... */];
    let result = service.compress(&messages, StrategyType::Semantic).await.unwrap();
    
    assert!(result.compressed);
}
```

### 问题 3: Mock LLM 测试失败

**症状**: 测试中 LLM 调用返回空响应

**解决方案**:

```rust
use mockall::predicate::*;
use mockall::mock;

mock! {
    LLMClient {}
    
    #[async_trait]
    impl LLMClient for LLMClient {
        async fn send_message(
            &self,
            messages: Vec<Message>
        ) -> anyhow::Result<String>;
    }
}

#[tokio::test]
async fn test_with_mock() {
    let mut mock = MockLLMClient::new();
    
    // 设置期望返回
    mock.expect_send_message()
        .returning(|_| Ok("压缩后的摘要".to_string()))
        .times(1);  // 期望调用 1 次
    
    let service = CompressionService::new(Arc::new(mock), CompressionConfig::default()).unwrap();
    
    let messages = vec![/* ... */];
    let result = service.compress(&messages, StrategyType::Semantic).await.unwrap();
    
    assert!(result.compressed);
}
```

---

## 生产环境问题

### 问题 1: 间歇性压缩失败

**症状**: 压缩操作偶尔失败，无明显规律

**诊断**:

```rust
use tracing::{info, warn, error, instrument};

#[instrument(skip(service))]
async fn compress_with_retry(
    service: &mut CompressionService,
    messages: &[Message],
    strategy: StrategyType,
    max_retries: usize
) -> anyhow::Result<CompressionResult> {
    let mut last_error = None;
    
    for attempt in 1..=max_retries {
        info!("压缩尝试 {}/{}", attempt, max_retries);
        
        match service.compress(messages, strategy).await {
            Ok(result) => {
                info!("压缩成功");
                return Ok(result);
            }
            Err(e) => {
                warn!("压缩失败 (尝试 {}): {}", attempt, e);
                last_error = Some(e);
                
                // 指数退避
                let delay = Duration::from_millis(100 * 2_u64.pow(attempt as u32 - 1));
                tokio::time::sleep(delay).await;
            }
        }
    }
    
    Err(last_error.unwrap())
}
```

### 问题 2: 数据竞争

**症状**: 并发场景下结果不一致

**诊断**:

```rust
use std::sync::Mutex;

// 使用 Mutex 保护共享状态
struct ThreadSafeService {
    service: Mutex<CompressionService>,
}

impl ThreadSafeService {
    pub async fn compress(
        &self,
        messages: &[Message],
        strategy: StrategyType
    ) -> anyhow::Result<CompressionResult> {
        let mut service = self.service.lock().unwrap();
        service.compress(messages, strategy).await
    }
}

// 或使用 tokio::sync::Mutex（异步友好）
struct AsyncThreadSafeService {
    service: tokio::sync::Mutex<CompressionService>,
}
```

---

## 诊断工具

### 1. 日志增强

```rust
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};

fn setup_logging() {
    tracing_subscriber::registry()
        .with(tracing_subscriber::EnvFilter::new(
            std::env::var("RUST_LOG")
                .unwrap_or_else(|_| "info,agent_context_compression=debug".into())
        ))
        .with(tracing_subscriber::fmt::layer())
        .init();
}
```

```bash
# 运行时启用详细日志
RUST_LOG=agent_context_compression=trace cargo run
```

### 2. 性能分析脚本

```rust
// benches/diagnostic.rs
use criterion::{black_box, criterion_group, criterion_main, Criterion};

fn diagnose_performance(c: &mut Criterion) {
    let counter = TokenCounter::new_for_claude().unwrap();
    
    let test_texts = vec![
        ("10_words", "Hello world this is a test message here."),
        ("100_words", /* ... 100 词文本 */),
        ("1000_words", /* ... 1000 词文本 */),
    ];
    
    for (name, text) in test_texts {
        c.bench_function(&format!("count_{}", name), |b| {
            b.iter(|| counter.count(black_box(text)))
        });
    }
    
    // 缓存性能
    let cache: Cache<String, usize> = Cache::new(1000);
    c.bench_function("cache_put", |b| {
        let mut i = 0;
        b.iter(|| {
            cache.put(format!("key_{}", i), black_box(42));
            i += 1;
        })
    });
    
    c.bench_function("cache_get_hit", |b| {
        cache.put("key".to_string(), 42);
        b.iter(|| cache.get(black_box("key")))
    });
}

criterion_group!(benches, diagnose_performance);
criterion_main!(benches);
```

```bash
# 运行诊断
cargo bench --bench diagnostic
```

### 3. 健康检查端点

```rust
use warp::Filter;

#[derive(Serialize)]
struct HealthStatus {
    status: String,
    cache_utilization: f64,
    history_count: usize,
    uptime_seconds: u64,
}

async fn health_check(
    service: Arc<Mutex<CompressionService>>,
    cache: Arc<Cache<String, usize>>,
    start_time: Instant
) -> Result<impl warp::Reply, warp::Rejection> {
    let service = service.lock().unwrap();
    
    let status = HealthStatus {
        status: "ok".to_string(),
        cache_utilization: cache.stats().utilization(),
        history_count: service.history().len(),
        uptime_seconds: start_time.elapsed().as_secs(),
    };
    
    Ok(warp::reply::json(&status))
}

// 启动健康检查服务
let health = warp::path("health")
    .and_then(move || health_check(service.clone(), cache.clone(), start_time));

warp::serve(health).run(([0, 0, 0, 0], 8080)).await;
```

```bash
# 检查健康状态
curl http://localhost:8080/health
```

---

## 常见错误码

| 错误码 | 说明 | 解决方案 |
|-------|------|---------|
| `CompressionError::TokenCountFailed` | Token 计数失败 | 检查 tiktoken 初始化 |
| `CompressionError::LLMCallFailed` | LLM 调用失败 | 检查 API 密钥和网络 |
| `CompressionError::InvalidConfig` | 配置无效 | 验证配置参数范围 |
| `CompressionError::CacheFull` | 缓存已满 | 增加缓存容量或清理 |
| `CompressionError::InvalidMessage` | 消息格式无效 | 检查消息角色和内容 |

---

## 获取帮助

如果以上方案未能解决您的问题，请：

1. **查看详细日志**:
   ```bash
   RUST_LOG=agent_context_compression=trace cargo run 2>&1 | tee debug.log
   ```

2. **收集诊断信息**:
   - Rust 版本: `rustc --version`
   - 依赖版本: `cargo tree | grep tiktoken`
   - 配置文件: `cat compression.toml`
   - 错误日志: `debug.log`

3. **提交 Issue**:
   - 包含错误信息
   - 包含最小可复现示例
   - 包含诊断信息

4. **参考其他文档**:
   - [使用指南](USAGE.md) - 详细的 API 使用说明
   - [最佳实践](BEST_PRACTICES.md) - 性能优化建议
   - [性能分析报告](V2_COMPRESSION_PERFORMANCE_ANALYSIS.md) - 基准测试数据

---

**文档版本**: 1.0  
**最后更新**: 2026-04-18
