use crate::models::CompressionConfig;
use crate::Result;
use serde::{Deserialize, Serialize};
use std::path::Path;

/// 滑动窗口配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SlidingWindowConfig {
    pub window_size: usize,
}

impl Default for SlidingWindowConfig {
    fn default() -> Self {
        Self { window_size: 10 }
    }
}

/// 语义压缩配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SemanticConfig {
    pub target_tokens: usize,
    pub model: String,
}

impl Default for SemanticConfig {
    fn default() -> Self {
        Self {
            target_tokens: 2000,
            model: "claude-3-5-sonnet-20241022".to_string(),
        }
    }
}

/// 分层压缩配置
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct HierarchicalConfig {
    pub small_threshold: usize,
    pub large_threshold: usize,
    pub large_token_threshold: usize,
}

impl Default for HierarchicalConfig {
    fn default() -> Self {
        Self {
            small_threshold: 20,
            large_threshold: 50,
            large_token_threshold: 8000,
        }
    }
}

/// 策略配置集合
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct StrategyConfigs {
    pub sliding_window: SlidingWindowConfig,
    pub semantic: SemanticConfig,
    pub hierarchical: HierarchicalConfig,
}

impl Default for StrategyConfigs {
    fn default() -> Self {
        Self {
            sliding_window: SlidingWindowConfig::default(),
            semantic: SemanticConfig::default(),
            hierarchical: HierarchicalConfig::default(),
        }
    }
}

/// 配置文件格式
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ConfigFile {
    pub compression: CompressionConfig,

    #[serde(default)]
    pub strategies: StrategyConfigs,
}

impl Default for ConfigFile {
    fn default() -> Self {
        Self {
            compression: CompressionConfig::default(),
            strategies: StrategyConfigs::default(),
        }
    }
}

impl ConfigFile {
    /// 从文件加载配置
    ///
    /// 支持 TOML 和 YAML 格式
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let config = ConfigFile::from_file("compression.toml")?;
    /// ```
    pub fn from_file<P: AsRef<Path>>(path: P) -> Result<Self> {
        let content = std::fs::read_to_string(&path).map_err(|e| {
            crate::CompressionError::InvalidConfig(format!(
                "Failed to read config file: {}",
                e
            ))
        })?;

        let ext = path
            .as_ref()
            .extension()
            .and_then(|s| s.to_str())
            .unwrap_or("toml");

        match ext {
            "toml" => toml::from_str(&content).map_err(|e| {
                crate::CompressionError::InvalidConfig(format!(
                    "Failed to parse TOML config: {}",
                    e
                ))
            }),
            "yaml" | "yml" => serde_yaml::from_str(&content).map_err(|e| {
                crate::CompressionError::InvalidConfig(format!(
                    "Failed to parse YAML config: {}",
                    e
                ))
            }),
            _ => Err(crate::CompressionError::InvalidConfig(format!(
                "Unsupported config file format: {}",
                ext
            ))),
        }
    }

    /// 保存配置到文件
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let config = ConfigFile::default();
    /// config.save_to_file("compression.toml")?;
    /// ```
    pub fn save_to_file<P: AsRef<Path>>(&self, path: P) -> Result<()> {
        let ext = path
            .as_ref()
            .extension()
            .and_then(|s| s.to_str())
            .unwrap_or("toml");

        let content = match ext {
            "toml" => toml::to_string_pretty(self).map_err(|e| {
                crate::CompressionError::InvalidConfig(format!(
                    "Failed to serialize to TOML: {}",
                    e
                ))
            })?,
            "yaml" | "yml" => serde_yaml::to_string(self).map_err(|e| {
                crate::CompressionError::InvalidConfig(format!(
                    "Failed to serialize to YAML: {}",
                    e
                ))
            })?,
            _ => {
                return Err(crate::CompressionError::InvalidConfig(format!(
                    "Unsupported config file format: {}",
                    ext
                )))
            }
        };

        std::fs::write(path, content).map_err(|e| {
            crate::CompressionError::InvalidConfig(format!("Failed to write config file: {}", e))
        })?;

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    #[test]
    fn test_default_config() {
        let config = ConfigFile::default();
        assert_eq!(config.compression.auto_trigger_threshold, 15);
        assert_eq!(config.strategies.sliding_window.window_size, 10);
        assert_eq!(config.strategies.semantic.target_tokens, 2000);
    }

    #[test]
    fn test_save_and_load_toml() {
        let temp_dir = TempDir::new().unwrap();
        let config_path = temp_dir.path().join("test_config.toml");

        // 创建并保存配置
        let original_config = ConfigFile::default();
        original_config.save_to_file(&config_path).unwrap();

        // 加载配置
        let loaded_config = ConfigFile::from_file(&config_path).unwrap();

        assert_eq!(
            original_config.compression.auto_trigger_threshold,
            loaded_config.compression.auto_trigger_threshold
        );
        assert_eq!(
            original_config.strategies.sliding_window.window_size,
            loaded_config.strategies.sliding_window.window_size
        );
    }

    #[test]
    fn test_save_and_load_yaml() {
        let temp_dir = TempDir::new().unwrap();
        let config_path = temp_dir.path().join("test_config.yaml");

        // 创建并保存配置
        let original_config = ConfigFile::default();
        original_config.save_to_file(&config_path).unwrap();

        // 加载配置
        let loaded_config = ConfigFile::from_file(&config_path).unwrap();

        assert_eq!(
            original_config.compression.auto_trigger_threshold,
            loaded_config.compression.auto_trigger_threshold
        );
    }

    #[test]
    fn test_invalid_format() {
        let temp_dir = TempDir::new().unwrap();
        let config_path = temp_dir.path().join("test_config.txt");

        let config = ConfigFile::default();
        let result = config.save_to_file(&config_path);

        assert!(result.is_err());
    }

    #[test]
    fn test_custom_config() {
        let mut config = ConfigFile::default();
        config.compression.auto_trigger_threshold = 20;
        config.strategies.sliding_window.window_size = 15;
        config.strategies.semantic.target_tokens = 3000;

        assert_eq!(config.compression.auto_trigger_threshold, 20);
        assert_eq!(config.strategies.sliding_window.window_size, 15);
        assert_eq!(config.strategies.semantic.target_tokens, 3000);
    }

    #[test]
    fn test_load_nonexistent_file() {
        let result = ConfigFile::from_file("nonexistent_file.toml");
        assert!(result.is_err());
    }
}
