//! 上下文压缩系统基准测试
//!
//! 使用 Criterion 测试各个组件的性能

use agent_context_compression::{Cache, TokenCounter};
use agent_core::models::{Message, MessageRole};
use criterion::{black_box, criterion_group, criterion_main, BenchmarkId, Criterion};
use std::sync::Arc;
use uuid::Uuid;

// ============================================================================
// 辅助函数
// ============================================================================

/// 创建测试消息
fn create_test_messages(count: usize) -> Vec<Message> {
    (0..count)
        .map(|i| {
            let role = if i % 2 == 0 {
                MessageRole::User
            } else {
                MessageRole::Assistant
            };
            Message {
                id: Uuid::new_v4(),
                session_id: Uuid::new_v4(),
                role,
                content: format!(
                    "这是测试消息 {}。包含一些内容以便进行 Token 计数测试。重复内容：{}",
                    i,
                    "测试 ".repeat(10)
                ),
                created_at: chrono::Utc::now(),
                metadata: None,
            }
        })
        .collect()
}

/// 创建长文本
fn create_long_text(words: usize) -> String {
    (0..words)
        .map(|i| format!("word{}", i))
        .collect::<Vec<_>>()
        .join(" ")
}

// ============================================================================
// Token 计数基准测试
// ============================================================================

fn bench_token_counting(c: &mut Criterion) {
    let mut group = c.benchmark_group("token_counting");

    let counter = TokenCounter::new_for_claude().expect("Failed to create token counter");

    // 不同长度的文本
    for size in [10, 100, 1000, 5000].iter() {
        let text = create_long_text(*size);

        group.bench_with_input(
            BenchmarkId::new("count_tokens", size),
            &text,
            |b, text| {
                b.iter(|| {
                    counter.count(black_box(text));
                });
            },
        );
    }

    // 批量计数
    let texts: Vec<String> = (0..100).map(|i| format!("Test message {}", i)).collect();
    group.bench_function("count_batch_100", |b| {
        b.iter(|| {
            for text in &texts {
                counter.count(black_box(text));
            }
        });
    });

    group.finish();
}

// ============================================================================
// LRU 缓存基准测试
// ============================================================================

fn bench_cache_operations(c: &mut Criterion) {
    let mut group = c.benchmark_group("cache_operations");

    // 不同缓存大小
    for capacity in [100, 1000, 10000].iter() {
        let cache: Cache<String, usize> = Cache::new(*capacity);

        // 插入性能
        group.bench_with_input(
            BenchmarkId::new("put", capacity),
            capacity,
            |b, _capacity| {
                let mut i = 0;
                b.iter(|| {
                    cache.put(black_box(format!("key_{}", i)), black_box(i));
                    i += 1;
                });
            },
        );

        // 预填充缓存
        for i in 0..*capacity {
            cache.put(format!("key_{}", i), i);
        }

        // 命中性能
        group.bench_with_input(
            BenchmarkId::new("get_hit", capacity),
            capacity,
            |b, _capacity| {
                let mut i = 0;
                b.iter(|| {
                    let key = format!("key_{}", i % *capacity);
                    cache.get(black_box(&key));
                    i += 1;
                });
            },
        );

        // 未命中性能
        group.bench_with_input(
            BenchmarkId::new("get_miss", capacity),
            capacity,
            |b, _capacity| {
                let mut i = 0;
                b.iter(|| {
                    let key = format!("miss_{}", i);
                    cache.get(black_box(&key));
                    i += 1;
                });
            },
        );
    }

    group.finish();
}

// ============================================================================
// 滑动窗口压缩基准测试
// ============================================================================

fn bench_sliding_window_compression(c: &mut Criterion) {
    let mut group = c.benchmark_group("sliding_window_compression");

    // 不同消息数量
    for count in [10, 50, 100, 200].iter() {
        let messages = create_test_messages(*count);

        group.bench_with_input(
            BenchmarkId::new("compress", count),
            &messages,
            |b, messages| {
                b.iter(|| {
                    // 模拟滑动窗口逻辑
                    let window_size = 20;
                    let start = if messages.len() > window_size {
                        messages.len() - window_size
                    } else {
                        0
                    };
                    black_box(&messages[start..]);
                });
            },
        );
    }

    group.finish();
}

// ============================================================================
// Token 计数缓存效果基准测试
// ============================================================================

fn bench_token_counting_with_cache(c: &mut Criterion) {
    let mut group = c.benchmark_group("token_counting_with_cache");

    let counter = TokenCounter::new_for_claude().expect("Failed to create token counter");
    let cache: Cache<String, usize> = Cache::new(1000);

    let texts: Vec<String> = (0..100)
        .map(|i| format!("Repeated message {}", i % 10))
        .collect();

    // 无缓存
    group.bench_function("without_cache", |b| {
        b.iter(|| {
            for text in &texts {
                counter.count(black_box(text));
            }
        });
    });

    // 有缓存
    group.bench_function("with_cache", |b| {
        b.iter(|| {
            for text in &texts {
                if let Some(count) = cache.get(text) {
                    black_box(count);
                } else {
                    let count = counter.count(black_box(text));
                    cache.put(text.clone(), count);
                    black_box(count);
                }
            }
        });
    });

    group.finish();
}

// ============================================================================
// 消息处理流水线基准测试
// ============================================================================

fn bench_message_processing_pipeline(c: &mut Criterion) {
    let mut group = c.benchmark_group("message_processing_pipeline");

    let counter = Arc::new(TokenCounter::new_for_claude().expect("Failed to create token counter"));
    let cache: Cache<String, usize> = Cache::new(1000);

    for count in [10, 50, 100].iter() {
        let messages = create_test_messages(*count);

        group.bench_with_input(
            BenchmarkId::new("full_pipeline", count),
            &messages,
            |b, messages| {
                b.iter(|| {
                    let mut total_tokens = 0;

                    for msg in messages {
                        // 检查缓存
                        let tokens = if let Some(cached) = cache.get(&msg.content) {
                            cached
                        } else {
                            let count = counter.count(&msg.content);
                            cache.put(msg.content.clone(), count);
                            count
                        };

                        total_tokens += tokens;
                    }

                    black_box(total_tokens);
                });
            },
        );
    }

    group.finish();
}

// ============================================================================
// 并发缓存操作基准测试
// ============================================================================

fn bench_concurrent_cache_operations(c: &mut Criterion) {
    let mut group = c.benchmark_group("concurrent_cache_operations");

    group.bench_function("single_thread", |b| {
        let cache: Cache<i32, i32> = Cache::new(1000);

        b.iter(|| {
            for i in 0..100 {
                cache.put(black_box(i), black_box(i * 2));
            }
            for i in 0..100 {
                cache.get(black_box(&i));
            }
        });
    });

    group.bench_function("multi_thread", |b| {
        use std::thread;

        let cache: Cache<i32, i32> = Cache::new(1000);

        b.iter(|| {
            let cache_clone1 = cache.clone();
            let cache_clone2 = cache.clone();

            let h1 = thread::spawn(move || {
                for i in 0..50 {
                    cache_clone1.put(black_box(i), black_box(i * 2));
                }
            });

            let h2 = thread::spawn(move || {
                for i in 50..100 {
                    cache_clone2.put(black_box(i), black_box(i * 2));
                }
            });

            h1.join().unwrap();
            h2.join().unwrap();
        });
    });

    group.finish();
}

// ============================================================================
// Criterion 配置
// ============================================================================

criterion_group!(
    benches,
    bench_token_counting,
    bench_cache_operations,
    bench_sliding_window_compression,
    bench_token_counting_with_cache,
    bench_message_processing_pipeline,
    bench_concurrent_cache_operations,
);

criterion_main!(benches);
