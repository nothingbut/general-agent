"""配置加载器"""
import yaml
import os
from pathlib import Path


def load_config():
    """加载配置文件"""
    config_path = Path("config.yaml")
    if not config_path.exists():
        return get_default_config()
    
    with open(config_path) as f:
        return yaml.safe_load(f)


def get_default_config():
    """默认配置"""
    return {
        "llm": {
            "provider": "ollama",
            "ollama": {
                "base_url": "http://localhost:11434",
                "model": "qwen2.5:7b",
                "timeout": 120
            }
        }
    }
