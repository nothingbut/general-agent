# Phase 8: LLM 配置和 @model 命令集成设计

**日期:** 2026-03-09
**版本:** 1.0

---

## 概述

Phase 8 的 Agent 系统需要支持灵活的 LLM 配置，允许：
- 为不同 Agent 配置不同的 LLM 模型
- 通过 @model 命令动态添加和切换模型
- 支持多种 LLM 提供商（OpenAI, Anthropic, Ollama, etc.）
- 与现有的 LLM 配置系统集成

---

## 架构设计

### 1. LLM 配置数据模型

```python
from dataclasses import dataclass, field
from enum import Enum
from typing import Dict, Any, Optional

class LLMProvider(Enum):
    """LLM 提供商"""
    OPENAI = "openai"
    ANTHROPIC = "anthropic"
    OLLAMA = "ollama"
    AZURE = "azure"
    CUSTOM = "custom"

@dataclass
class LLMModelConfig:
    """LLM 模型配置"""
    model_id: str                    # 唯一标识
    provider: LLMProvider            # 提供商
    model_name: str                  # 模型名称

    # API 配置
    api_key: Optional[str] = None
    api_base: Optional[str] = None
    api_version: Optional[str] = None

    # 模型参数
    temperature: float = 0.7
    max_tokens: int = 4096
    top_p: float = 1.0
    frequency_penalty: float = 0.0
    presence_penalty: float = 0.0

    # 超时和重试
    timeout: float = 120.0
    max_retries: int = 3

    # 成本控制
    cost_per_1k_input_tokens: float = 0.0
    cost_per_1k_output_tokens: float = 0.0
    max_cost_per_request: Optional[float] = None

    # 元数据
    description: str = ""
    tags: list[str] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

@dataclass
class LLMRegistry:
    """LLM 模型注册表"""
    models: Dict[str, LLMModelConfig] = field(default_factory=dict)
    default_model_id: Optional[str] = None

    def register(self, config: LLMModelConfig) -> None:
        """注册模型"""
        self.models[config.model_id] = config

        # 如果是第一个模型，设为默认
        if self.default_model_id is None:
            self.default_model_id = config.model_id

    def get(self, model_id: str) -> Optional[LLMModelConfig]:
        """获取模型配置"""
        return self.models.get(model_id)

    def list(self, provider: Optional[LLMProvider] = None) -> list[LLMModelConfig]:
        """列出模型"""
        if provider is None:
            return list(self.models.values())
        return [m for m in self.models.values() if m.provider == provider]

    def set_default(self, model_id: str) -> None:
        """设置默认模型"""
        if model_id not in self.models:
            raise ValueError(f"Model {model_id} not found")
        self.default_model_id = model_id
```

### 2. Agent 的 LLM 配置

```python
@dataclass
class AgentDefinition:
    """Agent 定义（更新版）"""
    agent_id: str
    name: str
    role: AgentRole
    capabilities: List[AgentCapability]

    # LLM 配置（支持多种方式）
    llm_model_id: Optional[str] = None           # 方式 1: 引用注册的模型 ID
    llm_config: Optional[LLMModelConfig] = None  # 方式 2: 直接配置
    llm_override: Dict[str, Any] = field(default_factory=dict)  # 方式 3: 覆盖参数

    # 系统提示词
    system_prompt: str = ""

    # 工作流集成
    workflow_id: Optional[str] = None

    # 元数据
    metadata: Dict[str, Any] = field(default_factory=dict)

    def get_effective_llm_config(self, registry: LLMRegistry) -> LLMModelConfig:
        """获取有效的 LLM 配置"""
        # 优先级：llm_config > llm_model_id > registry.default

        if self.llm_config is not None:
            # 方式 2: 直接配置
            config = self.llm_config
        elif self.llm_model_id is not None:
            # 方式 1: 引用模型 ID
            config = registry.get(self.llm_model_id)
            if config is None:
                raise ValueError(f"Model {self.llm_model_id} not found in registry")
        else:
            # 使用默认模型
            if registry.default_model_id is None:
                raise ValueError("No default model configured")
            config = registry.get(registry.default_model_id)

        # 应用覆盖参数（方式 3）
        if self.llm_override:
            config = self._apply_overrides(config, self.llm_override)

        return config

    def _apply_overrides(
        self,
        config: LLMModelConfig,
        overrides: Dict[str, Any]
    ) -> LLMModelConfig:
        """应用覆盖参数"""
        # 创建新的配置副本
        import copy
        new_config = copy.deepcopy(config)

        # 应用覆盖
        for key, value in overrides.items():
            if hasattr(new_config, key):
                setattr(new_config, key, value)

        return new_config
```

### 3. @model 命令集成

#### 3.1 命令设计

```python
"""
@model 命令支持以下操作：

1. 列出所有模型
   @model list
   @model list --provider openai

2. 添加新模型
   @model add gpt-4-turbo --provider openai --temperature 0.5
   @model add claude-3-opus --provider anthropic --max-tokens 8192
   @model add llama3 --provider ollama --api-base http://localhost:11434

3. 切换默认模型
   @model use gpt-4-turbo
   @model default claude-3-opus

4. 查看模型详情
   @model show gpt-4-turbo
   @model info claude-3-opus

5. 为 Agent 指定模型
   @model set researcher-001 --model gpt-4-turbo
   @model set writer-001 --model claude-3-opus --temperature 0.9

6. 删除模型
   @model remove gpt-3.5-turbo
"""

from typing import Optional, List
import argparse

class ModelCommandHandler:
    """@model 命令处理器"""

    def __init__(self, registry: LLMRegistry, coordinator: AgentCoordinator):
        self.registry = registry
        self.coordinator = coordinator

    async def handle(self, command: str) -> str:
        """处理 @model 命令"""
        parser = self._create_parser()

        try:
            args = parser.parse_args(command.split()[1:])  # 跳过 @model

            if args.subcommand == "list":
                return self._list_models(args.provider)
            elif args.subcommand == "add":
                return await self._add_model(args)
            elif args.subcommand == "use" or args.subcommand == "default":
                return self._set_default(args.model_id)
            elif args.subcommand == "show" or args.subcommand == "info":
                return self._show_model(args.model_id)
            elif args.subcommand == "set":
                return await self._set_agent_model(args)
            elif args.subcommand == "remove":
                return self._remove_model(args.model_id)
            else:
                return "Unknown subcommand"

        except Exception as e:
            return f"Error: {e}"

    def _create_parser(self) -> argparse.ArgumentParser:
        """创建命令解析器"""
        parser = argparse.ArgumentParser(prog="@model")
        subparsers = parser.add_subparsers(dest="subcommand")

        # list
        list_parser = subparsers.add_parser("list")
        list_parser.add_argument("--provider", type=str)

        # add
        add_parser = subparsers.add_parser("add")
        add_parser.add_argument("model_id", type=str)
        add_parser.add_argument("--provider", type=str, required=True)
        add_parser.add_argument("--model-name", type=str)
        add_parser.add_argument("--api-key", type=str)
        add_parser.add_argument("--api-base", type=str)
        add_parser.add_argument("--temperature", type=float, default=0.7)
        add_parser.add_argument("--max-tokens", type=int, default=4096)
        add_parser.add_argument("--description", type=str, default="")

        # use/default
        use_parser = subparsers.add_parser("use")
        use_parser.add_argument("model_id", type=str)

        default_parser = subparsers.add_parser("default")
        default_parser.add_argument("model_id", type=str)

        # show/info
        show_parser = subparsers.add_parser("show")
        show_parser.add_argument("model_id", type=str)

        info_parser = subparsers.add_parser("info")
        info_parser.add_argument("model_id", type=str)

        # set
        set_parser = subparsers.add_parser("set")
        set_parser.add_argument("agent_id", type=str)
        set_parser.add_argument("--model", type=str, required=True)
        set_parser.add_argument("--temperature", type=float)
        set_parser.add_argument("--max-tokens", type=int)

        # remove
        remove_parser = subparsers.add_parser("remove")
        remove_parser.add_argument("model_id", type=str)

        return parser

    def _list_models(self, provider: Optional[str]) -> str:
        """列出模型"""
        provider_enum = LLMProvider(provider) if provider else None
        models = self.registry.list(provider_enum)

        if not models:
            return "No models registered"

        lines = ["Available Models:", ""]
        for model in models:
            default_mark = "(*)" if model.model_id == self.registry.default_model_id else "   "
            lines.append(f"{default_mark} {model.model_id}")
            lines.append(f"    Provider: {model.provider.value}")
            lines.append(f"    Model: {model.model_name}")
            if model.description:
                lines.append(f"    Description: {model.description}")
            lines.append("")

        return "\n".join(lines)

    async def _add_model(self, args) -> str:
        """添加模型"""
        config = LLMModelConfig(
            model_id=args.model_id,
            provider=LLMProvider(args.provider),
            model_name=args.model_name or args.model_id,
            api_key=args.api_key,
            api_base=args.api_base,
            temperature=args.temperature,
            max_tokens=args.max_tokens,
            description=args.description
        )

        self.registry.register(config)

        # 持久化配置
        await self._save_registry()

        return f"Model '{args.model_id}' added successfully"

    def _set_default(self, model_id: str) -> str:
        """设置默认模型"""
        try:
            self.registry.set_default(model_id)
            return f"Default model set to '{model_id}'"
        except ValueError as e:
            return str(e)

    def _show_model(self, model_id: str) -> str:
        """显示模型详情"""
        config = self.registry.get(model_id)
        if config is None:
            return f"Model '{model_id}' not found"

        lines = [
            f"Model: {config.model_id}",
            f"Provider: {config.provider.value}",
            f"Model Name: {config.model_name}",
            "",
            "Parameters:",
            f"  Temperature: {config.temperature}",
            f"  Max Tokens: {config.max_tokens}",
            f"  Top P: {config.top_p}",
            "",
            "Settings:",
            f"  Timeout: {config.timeout}s",
            f"  Max Retries: {config.max_retries}",
        ]

        if config.description:
            lines.insert(3, f"Description: {config.description}")

        if config.api_base:
            lines.append(f"  API Base: {config.api_base}")

        return "\n".join(lines)

    async def _set_agent_model(self, args) -> str:
        """为 Agent 设置模型"""
        agent = self.coordinator.agents.get(args.agent_id)
        if agent is None:
            return f"Agent '{args.agent_id}' not found"

        # 更新 Agent 的 LLM 配置
        agent.definition.llm_model_id = args.model

        # 应用覆盖参数
        if args.temperature is not None:
            agent.definition.llm_override["temperature"] = args.temperature
        if args.max_tokens is not None:
            agent.definition.llm_override["max_tokens"] = args.max_tokens

        return f"Agent '{args.agent_id}' model set to '{args.model}'"

    def _remove_model(self, model_id: str) -> str:
        """删除模型"""
        if model_id not in self.registry.models:
            return f"Model '{model_id}' not found"

        # 检查是否是默认模型
        if model_id == self.registry.default_model_id:
            return "Cannot remove default model. Set another default first."

        # 检查是否有 Agent 在使用
        agents_using = [
            agent.instance_id
            for agent in self.coordinator.agents.values()
            if agent.definition.llm_model_id == model_id
        ]

        if agents_using:
            return f"Cannot remove model. In use by agents: {', '.join(agents_using)}"

        del self.registry.models[model_id]
        return f"Model '{model_id}' removed"

    async def _save_registry(self) -> None:
        """持久化配置到数据库"""
        # TODO: 保存到数据库
        pass
```

#### 3.2 与现有系统集成

```python
# 在 Router 中集成 @model 命令

class SimpleRouter:
    """更新后的 Router（支持 @model）"""

    def __init__(self):
        self.llm_registry = LLMRegistry()
        self.agent_coordinator = None  # 延迟初始化
        self.model_command_handler = None

    async def initialize_phase8(self, coordinator: AgentCoordinator):
        """初始化 Phase 8 支持"""
        self.agent_coordinator = coordinator
        self.model_command_handler = ModelCommandHandler(
            self.llm_registry,
            coordinator
        )

        # 加载默认模型配置
        await self._load_default_models()

    async def _load_default_models(self):
        """加载默认模型配置"""
        # 从配置文件加载
        from src.core.config_loader import load_config

        config = load_config()
        llm_config = config.get("llm", {})

        # 添加配置的模型
        if llm_config.get("provider") == "ollama":
            ollama_cfg = llm_config.get("ollama", {})
            self.llm_registry.register(LLMModelConfig(
                model_id="ollama-default",
                provider=LLMProvider.OLLAMA,
                model_name=ollama_cfg.get("model", "llama3"),
                api_base=ollama_cfg.get("base_url", "http://localhost:11434"),
                temperature=ollama_cfg.get("temperature", 0.7),
                timeout=ollama_cfg.get("timeout", 120.0),
                description="Default Ollama model"
            ))

    async def route(self, message: str, context: Dict[str, Any]) -> str:
        """路由消息（支持 @model）"""

        # 检查是否是 @model 命令
        if message.startswith("@model "):
            if self.model_command_handler:
                return await self.model_command_handler.handle(message)
            else:
                return "Phase 8 not initialized. Run initialize_phase8() first."

        # 原有的路由逻辑
        # ...
        pass
```

### 4. 使用示例

```python
async def demo_model_management():
    """演示 @model 命令的使用"""

    # 初始化系统
    router = SimpleRouter()
    coordinator = AgentCoordinator(...)
    await router.initialize_phase8(coordinator)

    # 1. 列出所有模型
    result = await router.route("@model list", {})
    print(result)
    # 输出:
    # Available Models:
    # (*) ollama-default
    #     Provider: ollama
    #     Model: llama3
    #     Description: Default Ollama model

    # 2. 添加 GPT-4
    result = await router.route(
        "@model add gpt-4-turbo --provider openai --temperature 0.5 --description 'GPT-4 Turbo'",
        {}
    )
    print(result)
    # 输出: Model 'gpt-4-turbo' added successfully

    # 3. 添加 Claude
    result = await router.route(
        "@model add claude-3-opus --provider anthropic --max-tokens 8192",
        {}
    )
    print(result)

    # 4. 查看模型详情
    result = await router.route("@model show gpt-4-turbo", {})
    print(result)
    # 输出:
    # Model: gpt-4-turbo
    # Provider: openai
    # Model Name: gpt-4-turbo
    # Description: GPT-4 Turbo
    # Parameters:
    #   Temperature: 0.5
    #   Max Tokens: 4096
    #   ...

    # 5. 切换默认模型
    result = await router.route("@model use gpt-4-turbo", {})
    print(result)
    # 输出: Default model set to 'gpt-4-turbo'

    # 6. 为特定 Agent 设置模型
    result = await router.route(
        "@model set researcher-001 --model claude-3-opus --temperature 0.9",
        {}
    )
    print(result)
    # 输出: Agent 'researcher-001' model set to 'claude-3-opus'
```

---

## 配置文件格式

支持通过配置文件预定义模型：

```yaml
# config/llm_models.yaml

models:
  - id: gpt-4-turbo
    provider: openai
    model_name: gpt-4-turbo-preview
    temperature: 0.7
    max_tokens: 4096
    description: "GPT-4 Turbo - Most capable OpenAI model"

  - id: claude-3-opus
    provider: anthropic
    model_name: claude-3-opus-20240229
    temperature: 0.7
    max_tokens: 4096
    description: "Claude 3 Opus - Best for complex tasks"

  - id: llama3-local
    provider: ollama
    model_name: llama3
    api_base: http://localhost:11434
    temperature: 0.7
    max_tokens: 2048
    description: "Local Llama 3 model"

default_model: gpt-4-turbo
```

---

## 数据库持久化

模型配置存储在数据库中：

```sql
-- 新增表：llm_models
CREATE TABLE llm_models (
    model_id TEXT PRIMARY KEY,
    provider TEXT NOT NULL,
    model_name TEXT NOT NULL,
    api_key TEXT,
    api_base TEXT,
    api_version TEXT,
    temperature REAL DEFAULT 0.7,
    max_tokens INTEGER DEFAULT 4096,
    top_p REAL DEFAULT 1.0,
    frequency_penalty REAL DEFAULT 0.0,
    presence_penalty REAL DEFAULT 0.0,
    timeout REAL DEFAULT 120.0,
    max_retries INTEGER DEFAULT 3,
    cost_per_1k_input_tokens REAL DEFAULT 0.0,
    cost_per_1k_output_tokens REAL DEFAULT 0.0,
    max_cost_per_request REAL,
    description TEXT,
    tags TEXT,  -- JSON array
    metadata TEXT,  -- JSON object
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Agent 的 LLM 配置
CREATE TABLE agent_llm_configs (
    agent_id TEXT PRIMARY KEY,
    model_id TEXT,
    llm_override TEXT,  -- JSON object
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (model_id) REFERENCES llm_models(model_id)
);

-- 默认模型配置
CREATE TABLE llm_settings (
    key TEXT PRIMARY KEY,
    value TEXT,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO llm_settings (key, value) VALUES ('default_model_id', NULL);
```

---

## 集成点

1. **Router 集成**: 在 `SimpleRouter` 中注册 @model 命令
2. **Agent 创建**: 从配置或注册表获取 LLM 配置
3. **监控集成**: 记录 LLM 调用的成本和性能
4. **审批集成**: 高成本模型调用需要审批

---

## 验收标准

- [ ] @model 命令的所有子命令正常工作
- [ ] 模型配置正确持久化到数据库
- [ ] Agent 可以使用指定的模型
- [ ] 模型切换立即生效
- [ ] 不存在的模型 ID 会报错
- [ ] 删除被使用的模型会被阻止
- [ ] 配置文件可以正确加载
- [ ] 与现有 LLM 客户端无缝集成

---

**设计完成时间:** 2026-03-09
**下一步:** 实现 LLM 配置系统
