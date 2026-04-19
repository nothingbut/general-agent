//! LRU 缓存模块 - 用于缓存 Token 计数和压缩结果
//!
//! 提供线程安全的 LRU 缓存，用于优化性能

use lru::LruCache;
use std::hash::Hash;
use std::num::NonZeroUsize;
use std::sync::{Arc, Mutex};

/// LRU 缓存包装器
///
/// 提供线程安全的 LRU 缓存实现
#[derive(Clone)]
pub struct Cache<K, V>
where
    K: Hash + Eq + Clone,
    V: Clone,
{
    inner: Arc<Mutex<LruCache<K, V>>>,
}

impl<K, V> Cache<K, V>
where
    K: Hash + Eq + Clone,
    V: Clone,
{
    /// 创建新的缓存实例
    ///
    /// # 参数
    /// - `capacity`: 缓存容量
    ///
    /// # 示例
    /// ```
    /// use agent_context_compression::Cache;
    ///
    /// let cache: Cache<String, i32> = Cache::new(100);
    /// ```
    pub fn new(capacity: usize) -> Self {
        let capacity = NonZeroUsize::new(capacity).unwrap_or(NonZeroUsize::new(1).unwrap());
        Self {
            inner: Arc::new(Mutex::new(LruCache::new(capacity))),
        }
    }

    /// 获取缓存值
    ///
    /// # 参数
    /// - `key`: 缓存键
    ///
    /// # 返回
    /// 如果存在则返回 `Some(value)`，否则返回 `None`
    pub fn get(&self, key: &K) -> Option<V> {
        self.inner.lock().unwrap().get(key).cloned()
    }

    /// 插入缓存值
    ///
    /// # 参数
    /// - `key`: 缓存键
    /// - `value`: 缓存值
    ///
    /// # 返回
    /// 如果替换了旧值，返回 `Some(old_value)`
    pub fn put(&self, key: K, value: V) -> Option<V> {
        self.inner.lock().unwrap().put(key, value)
    }

    /// 清空缓存
    pub fn clear(&self) {
        self.inner.lock().unwrap().clear();
    }

    /// 获取缓存大小
    pub fn len(&self) -> usize {
        self.inner.lock().unwrap().len()
    }

    /// 检查缓存是否为空
    pub fn is_empty(&self) -> bool {
        self.inner.lock().unwrap().is_empty()
    }

    /// 移除指定的缓存项
    pub fn pop(&self, key: &K) -> Option<V> {
        self.inner.lock().unwrap().pop(key)
    }

    /// 获取缓存命中率统计
    ///
    /// 返回 (hits, misses)
    pub fn stats(&self) -> CacheStats {
        CacheStats {
            size: self.len(),
            capacity: self.inner.lock().unwrap().cap().get(),
        }
    }
}

/// 缓存统计信息
#[derive(Debug, Clone)]
pub struct CacheStats {
    /// 当前缓存大小
    pub size: usize,
    /// 缓存容量
    pub capacity: usize,
}

impl CacheStats {
    /// 获取缓存使用率 (0.0 - 1.0)
    pub fn utilization(&self) -> f64 {
        if self.capacity == 0 {
            0.0
        } else {
            self.size as f64 / self.capacity as f64
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_cache_basic_operations() {
        let cache: Cache<String, i32> = Cache::new(3);

        // 插入
        cache.put("a".to_string(), 1);
        cache.put("b".to_string(), 2);
        cache.put("c".to_string(), 3);

        // 获取
        assert_eq!(cache.get(&"a".to_string()), Some(1));
        assert_eq!(cache.get(&"b".to_string()), Some(2));
        assert_eq!(cache.get(&"c".to_string()), Some(3));
        assert_eq!(cache.get(&"d".to_string()), None);

        // 检查大小
        assert_eq!(cache.len(), 3);
        assert!(!cache.is_empty());
    }

    #[test]
    fn test_cache_lru_eviction() {
        let cache: Cache<String, i32> = Cache::new(2);

        cache.put("a".to_string(), 1);
        cache.put("b".to_string(), 2);

        // 访问 a，使其成为最近使用
        assert_eq!(cache.get(&"a".to_string()), Some(1));

        // 插入 c，应该驱逐 b
        cache.put("c".to_string(), 3);

        assert_eq!(cache.get(&"a".to_string()), Some(1));
        assert_eq!(cache.get(&"b".to_string()), None);
        assert_eq!(cache.get(&"c".to_string()), Some(3));
    }

    #[test]
    fn test_cache_clear() {
        let cache: Cache<String, i32> = Cache::new(10);

        cache.put("a".to_string(), 1);
        cache.put("b".to_string(), 2);
        assert_eq!(cache.len(), 2);

        cache.clear();
        assert_eq!(cache.len(), 0);
        assert!(cache.is_empty());
    }

    #[test]
    fn test_cache_pop() {
        let cache: Cache<String, i32> = Cache::new(10);

        cache.put("a".to_string(), 1);
        cache.put("b".to_string(), 2);

        assert_eq!(cache.pop(&"a".to_string()), Some(1));
        assert_eq!(cache.get(&"a".to_string()), None);
        assert_eq!(cache.len(), 1);
    }

    #[test]
    fn test_cache_stats() {
        let cache: Cache<String, i32> = Cache::new(10);

        cache.put("a".to_string(), 1);
        cache.put("b".to_string(), 2);

        let stats = cache.stats();
        assert_eq!(stats.size, 2);
        assert_eq!(stats.capacity, 10);
        assert!((stats.utilization() - 0.2).abs() < 0.01);
    }

    #[test]
    fn test_cache_thread_safety() {
        use std::thread;

        let cache: Cache<i32, i32> = Cache::new(100);
        let cache_clone = cache.clone();

        let handle = thread::spawn(move || {
            for i in 0..50 {
                cache_clone.put(i, i * 2);
            }
        });

        for i in 50..100 {
            cache.put(i, i * 2);
        }

        handle.join().unwrap();

        assert_eq!(cache.len(), 100);
    }
}
