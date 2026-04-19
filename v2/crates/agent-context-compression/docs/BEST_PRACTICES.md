# 最佳实践

本文档提供 `agent-context-compression` 在生产环境中的性能调优建议和最佳实践。

## 目录

- [性能调优](#性能调优)
- [生产环境配置](#生产环境配置)
- [监控和告警](#监控和告警)
- [容量规划](#容量规划)
- [安全建议](#安全建议)
- [常见场景最佳实践](#常见场景最佳实践)

---

## 性能调优

### 缓存配置优化

#### 1. 选择合适的缓存容量

缓存容量直接影响命中率和内存占用：

```rust
use agent_context_compression::Cache;

// 根据场景选择容量
let cache: Cache<String, usize> = match deployment_type {
    DeploymentType::Development => Cache::new(1_000),      // 1K
    DeploymentType::SmallProd => Cache::new(5_000),        // 5K
    DeploymentType::MediumProd => Cache::new(10_000),      // 10K
    DeploymentType::LargeProd => Cache::new(50_000),       // 50K
    DeploymentType::Enterprise => Cache::new(100_000),     // 100K
};
```

**容量选择指南**:

| 用户数 | 并发请求 | 推荐容量 | 内存占用（估算） |
|-------|---------|---------|----------------|
| < 100 | < 10 | 1,000 | ~100 KB |
| 100-1K | 10-50 | 5,000 | ~500 KB |
| 1K-10K | 50-200 | 10,000 | ~1 MB |
| 10K-100K | 200-1000 | 50,000 | ~5 MB |
| > 100K | > 1000 | 100,000+ | > 10 MB |

#### 2. 监控缓存命中率

```rust
use std::time::{Duration, Instant};

struct CacheMonitor {
    cache: Cache<String, usize>,
    last_report: Instant,
    report_interval: Duration,
}

impl CacheMonitor {
    pub fn new(capacity: usize) -> Self {
        Self {
            cache: Cache::new(capacity),
            last_report: Instant::now(),
            report_interval: Duration::from_secs(60), // 每分钟报告
        }
    }
    
    pub fn check_and_report(&mut self) {
        if self.last_report.elapsed() >= self.report_interval {
            let stats = self.cache.stats();
            let utilization = stats.utilization();
            
            println!("缓存统计:");
            println!("  使用率: {:.2}%", utilization * 100.0);
            println!("  大小: {}/{}", stats.size, stats.capacity);
            
            // 使用率过高，建议扩容
            if utilization > 0.90 {
                eprintln!("警告: 缓存使用率 > 90%，建议扩容");
            }
            
            self.last_report = Instant::now();
        }
    }
}
```

#### 3. 缓存预热

对于常用内容（如系统提示词），提前加载到缓存：

```rust
fn warmup_cache(
    cache: &Cache<String, usize>,
    counter: &TokenCounter
) -> anyhow::Result<()> {
    // 常用系统消息
    let system_prompts = vec![
        "You are a helpful assistant.",
        "You are an expert in Rust programming.",
        "Please answer concisely.",
        // 添加更多常用提示词
    ];
    
    for prompt in system_prompts {
        let count = counter.count(prompt);
        cache.put(prompt.to_string(), count);
    }
    
    println!("缓存预热完成: {} 条记录", cache.stats().size);
    Ok(())
}
```

### 压缩策略选择

#### 1. 根据场景选择策略

```rust
use agent_context_compression::{StrategyType, CompressionConfig};

// 实时对话场景（低延迟优先）
let realtime_config = CompressionConfig {
    default_strategy: StrategyType::SlidingWindow,
    sliding_window_size: 15,  // 保留较少消息
    ..Default::default()
};

// 归档场景（压缩率优先）
let archive_config = CompressionConfig {
    default_strategy: StrategyType::Semantic,
    ..Default::default()
};

// 通用场景（平衡）
let balanced_config = CompressionConfig {
    default_strategy: StrategyType::Hierarchical,
    hierarchical_small_threshold: 20,
    hierarchical_large_threshold: 50,
    hierarchical_token_threshold: 8000,
    ..Default::default()
};
```

#### 2. 压缩策略性能对比

| 策略 | 延迟 | Token 节省 | CPU 使用 | 适用场景 |
|-----|------|-----------|---------|---------|
| 滑动窗口 | 270 ps | 低（20-40%） | 极低 | 实时对话、快速响应 |
| 语义压缩 | 2-5s | 高（60-80%） | 高（需 LLM） | 归档、批量处理 |
| 分层压缩 | 动态 | 中-高（40-70%） | 中 | 通用场景（推荐） |

### 并发优化

#### 1. 使用 Arc 共享缓存

```rust
use std::sync::Arc;
use agent_context_compression::{TokenCounter, Cache};

#[derive(Clone)]
struct SharedResources {
    counter: Arc<TokenCounter>,
    cache: Arc<Cache<String, usize>>,
}

impl SharedResources {
    pub fn new(cache_capacity: usize) -> anyhow::Result<Self> {
        Ok(Self {
            counter: Arc::new(TokenCounter::new_for_claude()?),
            cache: Arc::new(Cache::new(cache_capacity)),
        })
    }
}

// 在多个异步任务中共享
async fn handle_request(
    message: String,
    resources: SharedResources
) -> usize {
    if let Some(count) = resources.cache.get(&message) {
        return count;
    }
    
    let count = resources.counter.count(&message);
    resources.cache.put(message, count);
    count
}
```

#### 2. 避免缓存锁竞争

如果缓存成为性能瓶颈，考虑分片缓存：

```rust
use std::collections::hash_map::DefaultHasher;
use std::hash::{Hash, Hasher};

struct ShardedCache {
    shards: Vec<Cache<String, usize>>,
    shard_count: usize,
}

impl ShardedCache {
    pub fn new(total_capacity: usize, shard_count: usize) -> Self {
        let capacity_per_shard = total_capacity / shard_count;
        let shards = (0..shard_count)
            .map(|_| Cache::new(capacity_per_shard))
            .collect();
        
        Self { shards, shard_count }
    }
    
    fn get_shard(&self, key: &str) -> &Cache<String, usize> {
        let mut hasher = DefaultHasher::new();
        key.hash(&mut hasher);
        let hash = hasher.finish() as usize;
        &self.shards[hash % self.shard_count]
    }
    
    pub fn get(&self, key: &str) -> Option<usize> {
        self.get_shard(key).get(key)
    }
    
    pub fn put(&self, key: String, value: usize) {
        self.get_shard(&key).put(key, value);
    }
}

// 使用分片缓存
let sharded_cache = ShardedCache::new(100_000, 16); // 16 个分片
```

---

## 生产环境配置

### 推荐配置模板

#### 1. 低延迟场景（实时对话）

```toml
# compression.toml - 低延迟配置
auto_compress = true
token_threshold = 6000        # 较低阈值，提前压缩
compression_history_limit = 20
default_strategy = "sliding_window"

[strategies.sliding_window]
window_size = 15              # 保留较少消息
preserve_system = true

[strategies.hierarchical]
small_threshold = 15
large_threshold = 30
large_token_threshold = 6000
```

#### 2. 高并发场景（API 服务）

```toml
# compression.toml - 高并发配置
auto_compress = true
token_threshold = 8000
compression_history_limit = 100  # 更大的历史记录
default_strategy = "hierarchical"

[strategies.sliding_window]
window_size = 20
preserve_system = true

[strategies.hierarchical]
small_threshold = 20
large_threshold = 50
large_token_threshold = 8000
```

代码配置：

```rust
// 使用分片缓存优化并发
let sharded_cache = ShardedCache::new(
    100_000,  // 总容量
    16        // 16 个分片，降低锁竞争
);

// 连接池配置（如果使用外部 LLM 服务）
let llm_client = Arc::new(AnthropicClient::with_pool_size(
    api_key,
    50  // 连接池大小
));
```

#### 3. 成本优化场景（Token 节省）

```toml
# compression.toml - 成本优化配置
auto_compress = true
token_threshold = 5000        # 更激进的压缩阈值
compression_history_limit = 50
default_strategy = "semantic"  # 优先使用语义压缩

[strategies.semantic]
model = "claude-3-5-sonnet-20241022"
preserve_system = true

[strategies.hierarchical]
small_threshold = 15
large_threshold = 40
large_token_threshold = 5000
```

### 环境变量配置

```bash
# .env 文件
ANTHROPIC_API_KEY=sk-ant-xxx
COMPRESSION_CONFIG_PATH=/etc/agent/compression.toml
CACHE_CAPACITY=50000
LOG_LEVEL=info
METRICS_ENABLED=true
METRICS_PORT=9090
```

加载配置：

```rust
use std::env;
use agent_context_compression::ConfigFile;

fn load_production_config() -> anyhow::Result<CompressionConfig> {
    let config_path = env::var("COMPRESSION_CONFIG_PATH")
        .unwrap_or_else(|_| "compression.toml".to_string());
    
    let mut config_file = ConfigFile::from_file(&config_path)?;
    
    // 从环境变量覆盖缓存容量
    if let Ok(capacity) = env::var("CACHE_CAPACITY") {
        let capacity: usize = capacity.parse()?;
        // 配置缓存容量（如果有此选项）
    }
    
    Ok(config_file.config)
}
```

---

## 监控和告警

### 核心指标

#### 1. 压缩性能指标

```rust
use std::time::Instant;
use serde::{Serialize, Deserialize};

#[derive(Serialize, Deserialize)]
struct CompressionMetrics {
    // 性能指标
    total_compressions: u64,
    avg_compression_time_ms: f64,
    p95_compression_time_ms: f64,
    p99_compression_time_ms: f64,
    
    // 效果指标
    avg_compression_ratio: f64,
    total_tokens_saved: u64,
    
    // 策略分布
    sliding_window_count: u64,
    semantic_count: u64,
    hierarchical_count: u64,
    
    // 缓存指标
    cache_hit_rate: f64,
    cache_utilization: f64,
}

struct MetricsCollector {
    compression_times: Vec<f64>,
    compression_ratios: Vec<f64>,
    tokens_saved: u64,
    strategy_counts: HashMap<StrategyType, u64>,
}

impl MetricsCollector {
    pub fn record_compression(
        &mut self,
        duration_ms: f64,
        result: &CompressionResult
    ) {
        self.compression_times.push(duration_ms);
        self.compression_ratios.push(result.compression_ratio);
        self.tokens_saved += result.saved_tokens as u64;
        
        *self.strategy_counts.entry(result.strategy).or_insert(0) += 1;
    }
    
    pub fn generate_report(&self, cache: &Cache<String, usize>) -> CompressionMetrics {
        let mut times = self.compression_times.clone();
        times.sort_by(|a, b| a.partial_cmp(b).unwrap());
        
        let p95_index = (times.len() as f64 * 0.95) as usize;
        let p99_index = (times.len() as f64 * 0.99) as usize;
        
        CompressionMetrics {
            total_compressions: self.compression_times.len() as u64,
            avg_compression_time_ms: times.iter().sum::<f64>() / times.len() as f64,
            p95_compression_time_ms: times.get(p95_index).copied().unwrap_or(0.0),
            p99_compression_time_ms: times.get(p99_index).copied().unwrap_or(0.0),
            avg_compression_ratio: self.compression_ratios.iter().sum::<f64>() 
                / self.compression_ratios.len() as f64,
            total_tokens_saved: self.tokens_saved,
            sliding_window_count: *self.strategy_counts.get(&StrategyType::SlidingWindow).unwrap_or(&0),
            semantic_count: *self.strategy_counts.get(&StrategyType::Semantic).unwrap_or(&0),
            hierarchical_count: *self.strategy_counts.get(&StrategyType::Hierarchical).unwrap_or(&0),
            cache_hit_rate: 0.0,  // 需要单独计算
            cache_utilization: cache.stats().utilization(),
        }
    }
}
```

#### 2. Prometheus 集成

```rust
use prometheus::{
    Registry, Counter, Histogram, Gauge,
    HistogramOpts, Opts,
};

struct PrometheusMetrics {
    registry: Registry,
    compression_duration: Histogram,
    compression_ratio: Histogram,
    tokens_saved: Counter,
    cache_hit_rate: Gauge,
    cache_utilization: Gauge,
}

impl PrometheusMetrics {
    pub fn new() -> anyhow::Result<Self> {
        let registry = Registry::new();
        
        let compression_duration = Histogram::with_opts(
            HistogramOpts::new(
                "compression_duration_seconds",
                "Compression duration in seconds"
            ).buckets(vec![0.001, 0.01, 0.1, 1.0, 5.0, 10.0])
        )?;
        registry.register(Box::new(compression_duration.clone()))?;
        
        let compression_ratio = Histogram::with_opts(
            HistogramOpts::new(
                "compression_ratio",
                "Compression ratio (compressed/original)"
            ).buckets(vec![0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0])
        )?;
        registry.register(Box::new(compression_ratio.clone()))?;
        
        let tokens_saved = Counter::with_opts(
            Opts::new("tokens_saved_total", "Total tokens saved by compression")
        )?;
        registry.register(Box::new(tokens_saved.clone()))?;
        
        let cache_hit_rate = Gauge::with_opts(
            Opts::new("cache_hit_rate", "Cache hit rate (0-1)")
        )?;
        registry.register(Box::new(cache_hit_rate.clone()))?;
        
        let cache_utilization = Gauge::with_opts(
            Opts::new("cache_utilization", "Cache utilization (0-1)")
        )?;
        registry.register(Box::new(cache_utilization.clone()))?;
        
        Ok(Self {
            registry,
            compression_duration,
            compression_ratio,
            tokens_saved,
            cache_hit_rate,
            cache_utilization,
        })
    }
    
    pub fn record_compression(&self, duration_secs: f64, result: &CompressionResult) {
        self.compression_duration.observe(duration_secs);
        self.compression_ratio.observe(result.compression_ratio);
        self.tokens_saved.inc_by(result.saved_tokens as f64);
    }
    
    pub fn update_cache_metrics(&self, cache: &Cache<String, usize>, hit_rate: f64) {
        self.cache_hit_rate.set(hit_rate);
        self.cache_utilization.set(cache.stats().utilization());
    }
}
```

### 告警规则

#### Prometheus 告警配置

```yaml
# prometheus-alerts.yml
groups:
  - name: compression_alerts
    interval: 30s
    rules:
      # 压缩延迟过高
      - alert: HighCompressionLatency
        expr: histogram_quantile(0.95, compression_duration_seconds) > 5.0
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "压缩延迟过高"
          description: "P95 压缩延迟 > 5 秒，当前值: {{ $value }}s"
      
      # 缓存使用率过高
      - alert: HighCacheUtilization
        expr: cache_utilization > 0.95
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "缓存使用率过高"
          description: "缓存使用率 > 95%，当前值: {{ $value | humanizePercentage }}"
      
      # 缓存命中率过低
      - alert: LowCacheHitRate
        expr: cache_hit_rate < 0.5
        for: 10m
        labels:
          severity: info
        annotations:
          summary: "缓存命中率过低"
          description: "缓存命中率 < 50%，当前值: {{ $value | humanizePercentage }}"
      
      # Token 节省效果差
      - alert: LowTokenSavings
        expr: rate(tokens_saved_total[5m]) < 100
        for: 10m
        labels:
          severity: info
        annotations:
          summary: "Token 节省效果差"
          description: "每 5 分钟节省 < 100 tokens"
```

---

## 容量规划

### 计算资源需求

#### 1. 内存估算

```rust
// 单条消息的平均内存占用
const AVG_MESSAGE_SIZE: usize = 500;  // 500 字节
const AVG_TOKEN_COUNT: usize = 100;   // 100 tokens

// 缓存内存占用估算
fn estimate_cache_memory(capacity: usize) -> usize {
    // Key (String) + Value (usize) + 开销
    let entry_size = 64 + 8 + 32;  // ~104 字节/条
    capacity * entry_size
}

// 会话历史内存占用估算
fn estimate_session_memory(
    message_count: usize,
    avg_message_size: usize
) -> usize {
    message_count * avg_message_size
}

// 总内存需求
fn calculate_memory_requirements(
    concurrent_sessions: usize,
    avg_messages_per_session: usize,
    cache_capacity: usize
) -> usize {
    let session_memory = concurrent_sessions 
        * estimate_session_memory(avg_messages_per_session, AVG_MESSAGE_SIZE);
    let cache_memory = estimate_cache_memory(cache_capacity);
    let overhead = (session_memory + cache_memory) / 5;  // 20% 开销
    
    session_memory + cache_memory + overhead
}

// 示例
fn main() {
    let memory_mb = calculate_memory_requirements(
        1000,    // 1000 并发会话
        50,      // 每会话 50 条消息
        10_000   // 缓存容量 10K
    ) / 1_024 / 1_024;
    
    println!("估算内存需求: {} MB", memory_mb);
    // 输出: 约 25 MB
}
```

#### 2. CPU 需求估算

```rust
// 每秒处理的消息数
const MESSAGES_PER_SECOND: f64 = 100.0;

// 不同策略的 CPU 时间（微秒）
const SLIDING_WINDOW_CPU_US: f64 = 0.27 / 1_000.0;  // 270 ps
const TOKEN_COUNT_CPU_US: f64 = 50.0;               // 平均 50 µs
const SEMANTIC_COMPRESS_CPU_US: f64 = 3_000_000.0;  // 3 秒（包含 LLM 调用）

fn estimate_cpu_utilization(
    messages_per_second: f64,
    sliding_window_ratio: f64,  // 使用滑动窗口的比例
    semantic_ratio: f64          // 使用语义压缩的比例
) -> f64 {
    let sliding_cpu = messages_per_second * sliding_window_ratio * SLIDING_WINDOW_CPU_US;
    let semantic_cpu = messages_per_second * semantic_ratio * SEMANTIC_COMPRESS_CPU_US;
    let token_count_cpu = messages_per_second * TOKEN_COUNT_CPU_US;
    
    // CPU 使用率（假设单核）
    (sliding_cpu + semantic_cpu + token_count_cpu) / 1_000_000.0
}

// 示例
fn main() {
    let cpu_usage = estimate_cpu_utilization(
        100.0,  // 100 msg/s
        0.8,    // 80% 使用滑动窗口
        0.2     // 20% 使用语义压缩
    );
    
    println!("估算 CPU 使用率: {:.2}%", cpu_usage * 100.0);
}
```

### 扩展策略

#### 水平扩展

```rust
// 使用负载均衡器分发请求到多个实例
struct LoadBalancer {
    instances: Vec<CompressionServiceInstance>,
    current_index: AtomicUsize,
}

impl LoadBalancer {
    pub async fn compress(
        &self,
        messages: &[Message],
        strategy: StrategyType
    ) -> anyhow::Result<CompressionResult> {
        // 轮询选择实例
        let index = self.current_index.fetch_add(1, Ordering::Relaxed) 
            % self.instances.len();
        
        self.instances[index].compress(messages, strategy).await
    }
}
```

#### 缓存分层

```rust
// L1: 本地内存缓存（快）
// L2: 分布式缓存（Redis）
struct TieredCache {
    l1_cache: Cache<String, usize>,
    l2_cache: RedisCache,
}

impl TieredCache {
    pub async fn get(&self, key: &str) -> Option<usize> {
        // 先查 L1
        if let Some(value) = self.l1_cache.get(key) {
            return Some(value);
        }
        
        // 再查 L2
        if let Some(value) = self.l2_cache.get(key).await.ok()? {
            // 回填到 L1
            self.l1_cache.put(key.to_string(), value);
            return Some(value);
        }
        
        None
    }
}
```

---

## 安全建议

### 1. API 密钥管理

```rust
use secrecy::{Secret, ExposeSecret};

// 使用 secrecy crate 保护敏感信息
struct SecureConfig {
    api_key: Secret<String>,
}

impl SecureConfig {
    pub fn from_env() -> anyhow::Result<Self> {
        let api_key = std::env::var("ANTHROPIC_API_KEY")
            .map(Secret::new)
            .map_err(|_| anyhow::anyhow!("ANTHROPIC_API_KEY not set"))?;
        
        Ok(Self { api_key })
    }
    
    pub fn create_client(&self) -> AnthropicClient {
        // 只在必要时暴露密钥
        AnthropicClient::new(self.api_key.expose_secret().clone())
    }
}

// 不要记录敏感信息
println!("API Key: {}", "***REDACTED***");
```

### 2. 输入验证

```rust
// 验证消息内容，防止注入攻击
fn validate_message(message: &Message) -> anyhow::Result<()> {
    // 检查消息长度
    const MAX_CONTENT_LENGTH: usize = 1_000_000;  // 1 MB
    if message.content.len() > MAX_CONTENT_LENGTH {
        anyhow::bail!("消息内容过长: {} 字节", message.content.len());
    }
    
    // 检查角色字段
    const VALID_ROLES: &[&str] = &["user", "assistant", "system"];
    if !VALID_ROLES.contains(&message.role.as_str()) {
        anyhow::bail!("无效的角色: {}", message.role);
    }
    
    Ok(())
}

// 验证配置参数
fn validate_config(config: &CompressionConfig) -> anyhow::Result<()> {
    if config.token_threshold < 1000 || config.token_threshold > 100_000 {
        anyhow::bail!("token_threshold 必须在 1000-100000 之间");
    }
    
    if config.sliding_window_size < 5 || config.sliding_window_size > 200 {
        anyhow::bail!("sliding_window_size 必须在 5-200 之间");
    }
    
    Ok(())
}
```

### 3. 速率限制

```rust
use governor::{Quota, RateLimiter};
use std::num::NonZeroU32;

// 限制 LLM API 调用频率
struct RateLimitedCompression {
    service: CompressionService,
    limiter: RateLimiter<NotKeyed, InMemoryState, DefaultClock>,
}

impl RateLimitedCompression {
    pub fn new(service: CompressionService, max_requests_per_second: u32) -> Self {
        let quota = Quota::per_second(NonZeroU32::new(max_requests_per_second).unwrap());
        let limiter = RateLimiter::direct(quota);
        
        Self { service, limiter }
    }
    
    pub async fn compress(
        &mut self,
        messages: &[Message],
        strategy: StrategyType
    ) -> anyhow::Result<CompressionResult> {
        // 等待速率限制允许
        self.limiter.until_ready().await;
        
        // 执行压缩
        self.service.compress(messages, strategy).await
    }
}
```

### 4. 审计日志

```rust
use tracing::{info, warn, error};

// 记录关键操作
fn audit_log_compression(
    user_id: &str,
    strategy: StrategyType,
    result: &CompressionResult
) {
    info!(
        user_id = user_id,
        strategy = ?strategy,
        original_count = result.original_count,
        compressed_count = result.compressed_count,
        saved_tokens = result.saved_tokens,
        "Compression executed"
    );
}

// 记录异常情况
fn audit_log_error(user_id: &str, error: &anyhow::Error) {
    error!(
        user_id = user_id,
        error = %error,
        "Compression failed"
    );
}
```

---

## 常见场景最佳实践

### 场景 1: 高频短对话（客服机器人）

```rust
// 配置
let config = CompressionConfig {
    auto_compress: true,
    token_threshold: 4000,           // 较低阈值
    default_strategy: StrategyType::SlidingWindow,
    sliding_window_size: 10,         // 保留较少消息
    ..Default::default()
};

// 使用较大的缓存（常见问题重复度高）
let cache = Cache::new(20_000);

// 预热常见问题
warmup_with_faq(&cache, &counter)?;
```

### 场景 2: 长对话归档（客户历史记录）

```rust
// 配置
let config = CompressionConfig {
    auto_compress: false,            // 手动触发
    default_strategy: StrategyType::Semantic,
    ..Default::default()
};

// 批量归档
async fn archive_old_conversations(
    service: &mut CompressionService,
    cutoff_date: DateTime<Utc>
) -> anyhow::Result<()> {
    let old_conversations = fetch_old_conversations(cutoff_date)?;
    
    for conversation in old_conversations {
        // 使用语义压缩生成摘要
        let result = service.compress(
            &conversation.messages,
            StrategyType::Semantic
        ).await?;
        
        // 保存摘要
        save_archived_conversation(
            conversation.id,
            result.compressed_messages
        )?;
        
        // 删除原始消息
        delete_original_messages(conversation.id)?;
        
        println!("归档会话 {}: 节省 {} tokens", 
            conversation.id, result.saved_tokens);
    }
    
    Ok(())
}
```

### 场景 3: API 服务（通用场景）

```rust
// 配置
let config = CompressionConfig {
    auto_compress: true,
    token_threshold: 8000,
    default_strategy: StrategyType::Hierarchical,
    compression_history_limit: 100,
    ..Default::default()
};

// 共享资源
let shared = Arc::new(SharedResources::new(50_000)?);

// Prometheus 指标
let metrics = Arc::new(PrometheusMetrics::new()?);

// 处理请求
async fn handle_api_request(
    request: CompressionRequest,
    shared: Arc<SharedResources>,
    metrics: Arc<PrometheusMetrics>
) -> Result<CompressionResponse, ApiError> {
    let start = Instant::now();
    
    // 验证输入
    validate_messages(&request.messages)?;
    
    // 执行压缩
    let result = shared.service.compress(
        &request.messages,
        request.strategy
    ).await?;
    
    // 记录指标
    let duration = start.elapsed().as_secs_f64();
    metrics.record_compression(duration, &result);
    
    Ok(CompressionResponse {
        compressed_messages: result.compressed_messages,
        metadata: CompressionMetadata {
            original_count: result.original_count,
            compressed_count: result.compressed_count,
            compression_ratio: result.compression_ratio,
            saved_tokens: result.saved_tokens,
            duration_ms: duration * 1000.0,
        }
    })
}
```

---

## 总结

关键要点：

1. **缓存优化**: 选择合适的容量，监控命中率，考虑分片缓存
2. **策略选择**: 根据场景选择合适的压缩策略
3. **并发优化**: 使用 Arc 共享资源，避免锁竞争
4. **监控告警**: 收集关键指标，设置合理的告警规则
5. **容量规划**: 提前估算资源需求，制定扩展策略
6. **安全防护**: 保护敏感信息，验证输入，限制速率

---

**文档版本**: 1.0  
**最后更新**: 2026-04-18
