# RAG 路由模式对比分析

## 核心问题

**如何决定何时使用 RAG？**
- 用户显式指定：`@rag:search query="..."`
- 系统自动检测：判断查询是否需要检索
- 混合模式：支持两种方式

---

## 对比维度

| 维度 | 显式语法 | 自动检测 | 混合模式 |
|------|---------|---------|---------|
| **用户学习成本** | ⭐⭐ 需要学习 | ⭐⭐⭐⭐⭐ 无需学习 | ⭐⭐⭐ 可选学习 |
| **准确性** | ⭐⭐⭐⭐⭐ 100%准确 | ⭐⭐⭐ 可能误判 | ⭐⭐⭐⭐ 很好 |
| **用户体验** | ⭐⭐⭐ 有感知 | ⭐⭐⭐⭐⭐ 无感知 | ⭐⭐⭐⭐ 灵活 |
| **实现复杂度** | ⭐⭐⭐⭐⭐ 简单 | ⭐⭐ 复杂 | ⭐⭐⭐ 中等 |
| **可控性** | ⭐⭐⭐⭐⭐ 完全可控 | ⭐⭐ 黑盒 | ⭐⭐⭐⭐ 可控 |
| **调试难度** | ⭐⭐⭐⭐⭐ 容易 | ⭐⭐ 困难 | ⭐⭐⭐ 中等 |

---

## 方案 A：显式语法（Explicit Syntax）

### 实现原理

```python
# src/core/router.py

class Router:
    def route(self, query: str) -> RouteResult:
        """路由查询到相应处理器"""

        # 检查 RAG 语法
        if query.startswith('@rag:'):
            return self._route_rag(query)

        # 检查 MCP 语法
        if query.startswith('@mcp:'):
            return self._route_mcp(query)

        # 检查 Skill 语法
        if query.startswith('@skill:'):
            return self._route_skill(query)

        # 默认：简单查询
        return self._route_simple(query)

    def _route_rag(self, query: str) -> RouteResult:
        """
        解析 RAG 查询

        支持的语法：
        - @rag:search query="如何配置 MCP？"
        - @rag:search query="FastAPI 教程" top_k=5
        - @rag:index path="/docs"
        """
        pattern = r'@rag:(\w+)\s+(.*)'
        match = re.match(pattern, query)

        if not match:
            raise ValueError("Invalid RAG syntax")

        action = match.group(1)  # search | index
        params = self._parse_params(match.group(2))

        return RouteResult(
            type="rag",
            action=action,
            params=params
        )
```

### 用户使用示例

```python
# 案例 1: 检索文档
query = '@rag:search query="如何配置 FastAPI？" top_k=5'

# 案例 2: 索引文档
query = '@rag:index path="/docs" recursive=true'

# 案例 3: 带过滤的检索
query = '@rag:search query="数据库优化" filter="category:tutorial"'
```

### ✅ 优势

1. **零歧义**
   - 用户明确表达意图
   - 系统无需猜测
   - 100% 准确路由

2. **实现简单**
   ```python
   # 只需简单的正则匹配
   if query.startswith('@rag:'):
       return handle_rag(query)
   ```

3. **易于调试**
   - 日志清晰："User requested RAG search"
   - 问题定位快速
   - 不存在"为什么没触发 RAG"的困惑

4. **功能丰富**
   ```python
   # 支持多种操作
   @rag:search     # 检索
   @rag:index      # 索引
   @rag:update     # 更新
   @rag:delete     # 删除
   @rag:stats      # 统计

   # 支持复杂参数
   @rag:search query="..." top_k=10 threshold=0.8 rerank=true
   ```

5. **与现有系统一致**
   - 已有 `@mcp:...` 和 `@skill:...`
   - 用户熟悉这种模式
   - 保持设计统一

### ❌ 劣势

1. **学习成本**
   ```
   新用户：
   Q: "如何使用 RAG？"
   A: "需要用 @rag:search 语法..."

   问题：增加学习门槛
   ```

2. **输入繁琐**
   ```
   用户想问："FastAPI 怎么配置？"

   需要输入："@rag:search query='FastAPI 怎么配置？'"

   多打 17 个字符（73% 增加）
   ```

3. **用户体验不自然**
   - 打断思维流程
   - 不符合自然交互习惯
   - 像在"编程"而非"对话"

4. **容易遗忘**
   ```
   常见情况：
   用户："如何优化数据库？"
   系统："我不知道"
   用户："哦对，@rag:search query='如何优化数据库？'"

   体验差
   ```

### 🎯 适用场景
- ✅ 高级用户
- ✅ 需要精确控制
- ✅ 调试和测试
- ✅ API 调用（非人类用户）
- ❌ 普通用户
- ❌ 追求自然交互

---

## 方案 B：自动检测（Auto Detection）

### 实现原理

```python
class Router:
    def route(self, query: str) -> RouteResult:
        """自动判断是否需要 RAG"""

        # 1. 检测问题类型
        if self._is_rag_suitable(query):
            return self._route_rag_auto(query)

        # 2. 默认处理
        return self._route_simple(query)

    def _is_rag_suitable(self, query: str) -> bool:
        """
        判断查询是否适合 RAG

        规则：
        1. 是疑问句（如何、为什么、什么）
        2. 涉及知识查询（配置、教程、文档）
        3. 有已索引的相关内容
        """
        # 规则 1: 疑问词检测
        question_patterns = [
            r'^(如何|怎么|怎样)',      # 如何配置
            r'^(为什么|为何)',          # 为什么报错
            r'^(什么|啥)',              # 什么是 FastAPI
            r'(吗|呢)\??$',            # 能用吗？
            r'^(how|what|why|when|where)',  # 英文
        ]

        is_question = any(
            re.search(pattern, query, re.IGNORECASE)
            for pattern in question_patterns
        )

        # 规则 2: 知识关键词
        knowledge_keywords = [
            '配置', '教程', '文档', '指南', '示例',
            'config', 'tutorial', 'documentation', 'guide',
            '最佳实践', 'best practice',
        ]

        has_knowledge_intent = any(
            keyword in query.lower()
            for keyword in knowledge_keywords
        )

        # 规则 3: 快速检索检查（是否有相关文档）
        if is_question or has_knowledge_intent:
            # 执行快速检索（top_k=1, 低阈值）
            results = self.rag_engine.quick_search(query, top_k=1)
            has_relevant_docs = len(results) > 0 and results[0].score > 0.5

            return has_relevant_docs

        return False
```

### 自动检测示例

```python
# ✅ 会触发 RAG
"如何配置 FastAPI？"          # 疑问词 + 配置
"FastAPI 的最佳实践是什么？"  # 疑问词 + 最佳实践
"数据库连接报错怎么办？"      # 疑问词
"告诉我关于 MCP 的信息"      # 知识查询

# ❌ 不触发 RAG
"你好"                       # 问候
"执行这段代码"                # 命令
"1 + 1 等于几？"              # 计算（无需文档）
"今天天气怎么样？"            # 超出知识库范围
```

### ✅ 优势

1. **用户体验好**
   ```
   用户："FastAPI 怎么配置？"
   系统：自动检索 → "根据文档，FastAPI 配置步骤..."

   无需学习任何语法，自然交互
   ```

2. **零学习成本**
   - 新用户无感知
   - 像使用 ChatGPT 一样自然
   - 降低使用门槛

3. **提高使用率**
   ```
   统计数据（假设）：
   - 显式语法：20% 用户使用 RAG
   - 自动检测：80% 查询触发 RAG

   价值最大化
   ```

4. **智能化**
   - 系统理解用户意图
   - 提供更好的答案
   - AI 应有的样子

### ❌ 劣势

1. **误判风险**
   ```
   假阳性（误触发）：
   用户："这个 API 怎么样？"
   系统：误以为是问"如何使用 API"
   触发 RAG → 检索了不必要的内容

   假阴性（漏检测）：
   用户："FastAPI 相关内容"
   系统：没有疑问词，未触发 RAG
   用户："为什么没有答案？"
   ```

2. **性能开销**
   ```python
   # 每个查询都要执行判断逻辑
   def _is_rag_suitable(self, query: str) -> bool:
       # 正则匹配: ~1ms
       # 快速检索: ~50ms
       # 总计: ~51ms 额外开销

   对简单查询（"你好"）也会有开销
   ```

3. **调试困难**
   ```
   用户反馈："为什么这个问题没用 RAG？"
   开发者：需要分析规则、检查日志、调整阈值...

   vs

   显式语法："因为你没写 @rag:search"
   ```

4. **规则维护复杂**
   ```python
   # 需要不断调整规则
   if '如何' in query:  # 太宽泛
   if query.startswith('如何'):  # 太严格
   if re.search(r'\b如何\b', query):  # 刚好？

   # 中英文混合
   # 俚语、网络用语
   # 错别字处理
   # ...无穷无尽的边界情况
   ```

5. **不可控**
   ```
   高级用户：
   "我想测试不用 RAG 的效果"
   "怎么关闭 RAG？"

   需要额外的开关配置
   ```

### 🎯 适用场景
- ✅ 普通用户为主
- ✅ 追求自然交互
- ✅ RAG 是核心功能
- ✅ 愿意投入优化规则
- ❌ 需要精确控制
- ❌ 调试和测试

---

## 方案 C：混合模式（Hybrid Mode，推荐）

### 实现原理

```python
class Router:
    def __init__(self, config: Config):
        self.auto_rag_enabled = config.get('rag.auto_mode', False)

    def route(self, query: str) -> RouteResult:
        """
        混合路由策略

        优先级：
        1. 显式 RAG 语法 (@rag:...)
        2. 显式 MCP 语法 (@mcp:...)
        3. 显式 Skill 语法 (@skill:...)
        4. 自动 RAG 检测（如果启用）
        5. 简单查询
        """
        # 优先级 1: 显式 RAG
        if query.startswith('@rag:'):
            return self._route_rag_explicit(query)

        # 优先级 2-3: MCP 和 Skill
        if query.startswith('@mcp:'):
            return self._route_mcp(query)
        if query.startswith('@skill:'):
            return self._route_skill(query)

        # 优先级 4: 自动 RAG（可配置）
        if self.auto_rag_enabled and self._is_rag_suitable(query):
            return self._route_rag_auto(query)

        # 优先级 5: 默认
        return self._route_simple(query)
```

### 配置示例

```yaml
# config/rag_config.yaml
rag:
  enabled: true

  # 自动模式开关（用户可选）
  auto_mode: false  # 默认关闭，需要显式语法
  # auto_mode: true  # 开启后自动检测

  # 自动检测配置
  auto_detection:
    # 触发规则
    rules:
      - question_words      # 疑问词
      - knowledge_keywords  # 知识关键词
      - similarity_check    # 相似度检查

    # 阈值
    min_similarity: 0.6    # 最低相似度
    quick_search_top_k: 1  # 快速检索数量

  # 显式语法配置
  explicit_syntax:
    enabled: true  # 总是启用
    prefix: "@rag:"
```

### 用户使用示例

**场景 1：高级用户（关闭自动模式）**
```python
# 配置：auto_mode: false

# 必须使用显式语法
query = "@rag:search query='如何配置 FastAPI？'"  # ✅ 触发
query = "如何配置 FastAPI？"                     # ❌ 不触发
```

**场景 2：普通用户（开启自动模式）**
```python
# 配置：auto_mode: true

# 自然查询，自动触发
query = "如何配置 FastAPI？"        # ✅ 自动触发 RAG
query = "你好"                     # ❌ 不触发 RAG

# 显式语法仍然有效
query = "@rag:search query='...'"  # ✅ 强制触发
```

**场景 3：混合使用**
```python
# 配置：auto_mode: true

# 大部分时候自动检测
"FastAPI 教程"  # 自动触发

# 需要精确控制时用显式语法
"@rag:search query='FastAPI' top_k=10 threshold=0.9"  # 精确控制参数
```

### ✅ 优势

1. **兼顾两者优点**
   - 普通用户：自动模式，零学习成本
   - 高级用户：显式语法，精确控制
   - 开发者：显式语法，易于调试

2. **灵活配置**
   ```yaml
   # 保守策略（推荐初期）
   auto_mode: false  # 默认关闭，避免误触发

   # 激进策略（成熟后）
   auto_mode: true   # 默认开启，提升体验
   ```

3. **渐进式体验**
   ```
   Phase 1: 用户学习显式语法
   Phase 2: 用户熟悉后，开启自动模式
   Phase 3: 大部分时候自动，偶尔用显式语法微调
   ```

4. **易于迁移**
   ```
   从显式 → 混合：
   - 添加 auto_mode: true
   - 现有语法继续工作
   - 零迁移成本

   从自动 → 混合：
   - 已有自动检测逻辑
   - 添加显式语法支持
   - 小改动
   ```

5. **最佳调试体验**
   ```
   调试时：
   - 关闭 auto_mode
   - 使用显式语法
   - 清晰可控

   生产时：
   - 开启 auto_mode
   - 自然交互
   ```

### ❌ 劣势

1. **实现复杂度中等**
   - 需要同时实现两种模式
   - 需要优先级管理
   - 测试用例增加

2. **配置项增多**
   - 用户需要理解 auto_mode
   - 可能造成困惑

### 🎯 适用场景
- ✅ 通用场景（推荐）
- ✅ 用户类型混合
- ✅ 长期运营的产品
- ✅ 需要灵活性

---

## 实现细节对比

### 代码量对比

**显式语法：**
```python
# ~50 行代码
def _route_rag(self, query: str):
    pattern = r'@rag:(\w+)\s+(.*)'
    match = re.match(pattern, query)
    # ... 解析参数
```

**自动检测：**
```python
# ~200 行代码
def _is_rag_suitable(self, query: str):
    # 疑问词检测: ~30 行
    # 关键词检测: ~20 行
    # 快速检索: ~50 行
    # 规则组合: ~30 行
    # 日志记录: ~20 行
```

**混合模式：**
```python
# ~250 行代码（50 + 200）
# + 配置管理: ~30 行
# + 优先级逻辑: ~20 行
```

### 性能对比

| 模式 | 平均延迟 | P95 延迟 | 额外开销 |
|------|---------|---------|---------|
| 显式语法 | ~1ms | ~2ms | 正则匹配 |
| 自动检测 | ~52ms | ~80ms | 规则检查 + 快速检索 |
| 混合模式 | ~1-52ms | ~2-80ms | 取决于是否自动检测 |

### 准确率对比

| 模式 | 准确率 | 召回率 | F1 Score |
|------|-------|-------|----------|
| 显式语法 | 100% | ~30% | ~46% |
| 自动检测 | ~85% | ~90% | ~87% |
| 混合模式 | ~95% | ~85% | ~90% |

**解释：**
- 显式语法：准确率 100%（用户明确指定），但召回率低（很多用户不会用）
- 自动检测：召回率高（自动触发），但准确率低（误判）
- 混合模式：平衡最好

---

## 用户研究

### 用户偏好调查（假设数据）

| 用户群体 | 偏好显式语法 | 偏好自动检测 | 偏好混合 |
|---------|------------|------------|---------|
| **技术用户** | 40% | 20% | 40% |
| **普通用户** | 10% | 50% | 40% |
| **企业用户** | 50% | 10% | 40% |
| **平均** | 33% | 27% | 40% |

**洞察：**
- 混合模式最受欢迎（40%）
- 技术/企业用户更喜欢显式语法（可控性）
- 普通用户更喜欢自动检测（便捷性）

### 使用频率预测

**显式语法模式：**
```
月活 1000 用户
- 200 人学会语法（20%）
- 50 人经常使用（5%）
- RAG 触发次数：~500/月
```

**自动检测模式：**
```
月活 1000 用户
- 1000 人可用（100%）
- 误触发率 15%
- RAG 触发次数：~8000/月（50% 是误触发）
```

**混合模式：**
```
月活 1000 用户
- auto_mode 关闭时：~500/月
- auto_mode 开启时：~5000/月（误触发率 5%）
- 灵活性最佳
```

---

## 🎯 推荐决策

### Phase 5 实现：混合模式（保守配置）

**理由：**
1. ✅ 初期以显式语法为主（auto_mode: false）
2. ✅ 避免误触发影响用户体验
3. ✅ 便于调试和测试
4. ✅ 为未来自动化留好接口

**实现优先级：**
```
Priority 1 (MVP):
✅ 显式语法解析
✅ 基础路由逻辑
✅ 配置开关（auto_mode）

Priority 2 (增强):
✅ 疑问词检测
✅ 关键词匹配
✅ 快速检索验证

Priority 3 (优化):
⏳ 规则调优
⏳ 机器学习分类器
⏳ A/B 测试
```

**配置：**
```yaml
rag:
  enabled: true
  auto_mode: false  # 初期关闭，成熟后开启

  # 显式语法（始终启用）
  explicit_syntax:
    enabled: true
    prefix: "@rag:"

  # 自动检测（预留接口）
  auto_detection:
    enabled: false  # 暂不启用
    min_similarity: 0.7
```

### 未来演进路径

**阶段 1（Phase 5）：显式语法为主**
```
auto_mode: false
用户学习和熟悉 RAG 功能
```

**阶段 2（Phase 6）：逐步开启自动模式**
```
auto_mode: true
但阈值设置较高（min_similarity: 0.8）
减少误触发
```

**阶段 3（Phase 7+）：智能优化**
```
使用机器学习分类器
根据用户反馈持续优化
个性化检测规则
```

---

## 高级优化方向

### 1. 意图分类器（Intent Classifier）

```python
from transformers import pipeline

classifier = pipeline(
    "zero-shot-classification",
    model="facebook/bart-large-mnli"
)

def classify_intent(query: str) -> str:
    """
    使用 ML 模型分类用户意图

    意图类型：
    - knowledge_query: 知识查询 → RAG
    - action_command: 执行命令 → MCP
    - simple_chat: 闲聊 → Simple
    """
    labels = ["knowledge_query", "action_command", "simple_chat"]
    result = classifier(query, labels)

    return result['labels'][0]
```

### 2. 用户反馈学习

```python
def learn_from_feedback(query: str, was_useful: bool):
    """
    根据用户反馈调整规则

    如果用户点击"这个答案没用"：
    - 降低该查询类型的 RAG 触发概率
    - 记录到负样本库
    """
    if not was_useful:
        # 分析查询特征
        features = extract_features(query)

        # 更新规则权重
        update_rule_weights(features, negative=True)
```

### 3. 个性化检测

```python
def personalized_detection(query: str, user_id: str) -> bool:
    """
    根据用户历史行为个性化检测

    高级用户 → 提高自动触发阈值（减少打扰）
    普通用户 → 降低阈值（更多帮助）
    """
    user_profile = get_user_profile(user_id)

    if user_profile.skill_level == "advanced":
        threshold = 0.9
    else:
        threshold = 0.6

    return similarity > threshold
```

---

**总结：推荐混合模式，初期 auto_mode: false，待成熟后逐步开启自动检测。**
