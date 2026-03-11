# 技能系统手工验收指南

## 快速开始

### 1. 编译可执行文件

```bash
cd v2/crates/agent-skills
cargo build --release --bin skills-cli
```

编译后的可执行文件位于：
```
v2/target/release/skills-cli
```

### 2. 安装到系统（可选）

```bash
# macOS/Linux
sudo cp target/release/skills-cli /usr/local/bin/

# 或者添加到 PATH
export PATH="$PATH:$(pwd)/target/release"
```

### 3. 运行验收测试

```bash
# 运行自动演示（推荐）
skills-cli demo

# 进入交互模式
skills-cli interactive

# 显示帮助
skills-cli help
```

## 验收测试清单

### ✅ 功能验收

#### 1. 技能加载 (SkillLoader)
- [ ] 成功加载所有 .md 文件
- [ ] 正确提取命名空间（包括嵌套）
- [ ] 跳过非 .md 文件
- [ ] 支持 .ignore 文件过滤

**验证方法:** 运行 `skills-cli demo`，查看"步骤 1"输出
```
✅ 成功加载 4 个技能:
   - work:email:reply (回复工作邮件)
   - work:greeting (正式的工作问候)
   - task:create (创建新任务)
   - greeting (友好的问候)
```

#### 2. 技能解析 (SkillParser)
- [ ] 正确解析 YAML frontmatter
- [ ] 提取 Markdown 内容
- [ ] 解析参数定义（必需/可选/默认值）
- [ ] 错误处理（缺少字段、无效 YAML）

**验证方法:** 查看"步骤 5"输出，确认参数信息正确
```
- task:create (创建新任务)
  参数:
    - title (必需): 任务标题
    - priority (可选): 任务优先级, 默认: medium
```

#### 3. 技能注册 (SkillRegistry)
- [ ] 成功注册所有技能
- [ ] 支持完整名称查询（namespace:name）
- [ ] 支持短名称查询（无歧义时）
- [ ] 检测并报告歧义
- [ ] 按命名空间查询

**验证方法:** 查看"步骤 2"和"步骤 5"输出

#### 4. 调用解析 (SkillExecutor - parse_invocation)
- [ ] 支持 @ 前缀
- [ ] 支持 / 前缀
- [ ] 支持命名空间（namespace:skill）
- [ ] 提取单引号参数
- [ ] 提取双引号参数
- [ ] 支持参数值含空格

**验证方法:** 查看"步骤 3"每个测试的解析输出
```
✅ 解析成功: skill=greeting, params={"user_name": "Alice"}
✅ 解析成功: skill=work:email:reply, params={...}
```

#### 5. 技能执行 (SkillExecutor - execute)
- [ ] 验证必需参数
- [ ] 应用默认值
- [ ] 替换占位符 {param}
- [ ] 生成正确的提示词

**验证方法:** 查看"步骤 3"每个测试的执行结果
```
测试 1: 基本问候
  ✅ 执行成功:
     你好 Alice！我将以friendly的方式为您服务。
```

#### 6. 错误处理
- [ ] 缺少必需参数 → ValidationError
- [ ] 技能不存在 → SkillNotFound
- [ ] 命名歧义 → AmbiguousSkillName
- [ ] 无效语法 → InvalidSyntax

**验证方法:** 查看"步骤 4"错误测试输出
```
测试: 缺少必需参数
  ✅ 正确失败: Validation error: Required parameter 'user_name' is missing

测试: 技能不存在
  ✅ 正确失败: Skill not found: nonexistent
```

### ✅ 性能验收

#### 基准测试
- [ ] 单个技能解析 < 1ms
- [ ] 100 个技能加载 < 100ms
- [ ] 技能查询 O(1) 复杂度

**验证方法:** 使用内置演示已足够，如需精确测试：
```bash
cargo bench --bench skills_benchmark  # 如果有 benchmark
```

### ✅ 代码质量验收

#### 测试覆盖
```bash
cargo test
```

期望输出：
```
test result: ok. 66 passed; 0 failed
  - 单元测试: 60 个
  - 集成测试: 6 个
```

#### 代码检查
```bash
# Clippy 检查
cargo clippy -- -D warnings

# 格式检查
cargo fmt --check
```

期望：无警告，无错误

#### 文档完整性
- [ ] README.md 存在且完整
- [ ] 每个公共 API 有文档注释
- [ ] 示例代码可运行

```bash
# 生成文档
cargo doc --open

# 运行示例
cargo run --example basic_usage
```

## 交互式验收

运行交互模式进行手工测试：

```bash
skills-cli interactive
```

### 测试场景

#### 场景 1: 基本调用
```
> @greeting user_name='Alice'
```
期望：输出问候消息

#### 场景 2: 使用默认值
```
> @task:create title='Fix bug'
```
期望：使用默认 priority='medium'

#### 场景 3: 命名空间调用
```
> @work:email:reply recipient='Bob' message='收到'
```
期望：生成邮件回复

#### 场景 4: 列出技能
```
> list
```
期望：显示所有 4 个技能

#### 场景 5: 错误处理
```
> @greeting
```
期望：报错"缺少必需参数 user_name"

## 完整验收流程

### 第一步：编译
```bash
cd v2/crates/agent-skills
cargo build --release --bin skills-cli
```

### 第二步：运行自动演示
```bash
./target/release/skills-cli demo
```

检查所有步骤是否通过：
- ✅ 步骤 1: 加载技能
- ✅ 步骤 2: 注册技能
- ✅ 步骤 3: 执行测试
- ✅ 步骤 4: 错误处理
- ✅ 步骤 5: 列出技能

### 第三步：交互式测试
```bash
./target/release/skills-cli interactive
```

执行上述 5 个测试场景。

### 第四步：运行所有测试
```bash
cargo test
```

确认 66 个测试全部通过。

### 第五步：代码质量检查
```bash
cargo clippy -- -D warnings
cargo fmt --check
```

确认无警告无错误。

## 验收标准

### 必须满足（🔴 Critical）
- [x] 所有 66 个测试通过
- [x] 自动演示完全通过
- [x] 无 clippy 警告
- [x] 代码格式正确

### 应该满足（🟡 Important）
- [x] 交互式测试通过
- [x] 文档完整
- [x] 示例可运行

### 可选（🟢 Nice to Have）
- [ ] 性能基准测试
- [ ] 覆盖率报告 > 80%
- [ ] API 文档生成

## 问题排查

### 编译失败
```bash
# 清理并重新编译
cargo clean
cargo build --release --bin skills-cli
```

### 运行时错误
```bash
# 检查版本
cargo --version  # 需要 Rust 1.70+

# 查看详细错误
RUST_BACKTRACE=1 skills-cli demo
```

### 测试失败
```bash
# 运行特定测试
cargo test test_name -- --nocapture

# 查看测试输出
cargo test -- --show-output
```

## 验收报告模板

```
技能系统验收报告
==================

日期: ________
验收人: ________

1. 功能验收
   - [ ] 技能加载: ____
   - [ ] 技能解析: ____
   - [ ] 技能注册: ____
   - [ ] 调用解析: ____
   - [ ] 技能执行: ____
   - [ ] 错误处理: ____

2. 测试验收
   - [ ] 单元测试: __/60 通过
   - [ ] 集成测试: __/6 通过
   - [ ] 总计: __/66 通过

3. 代码质量
   - [ ] Clippy: ____
   - [ ] Fmt: ____
   - [ ] 文档: ____

4. 交互式测试
   - [ ] 场景 1: ____
   - [ ] 场景 2: ____
   - [ ] 场景 3: ____
   - [ ] 场景 4: ____
   - [ ] 场景 5: ____

结论: [ ] 通过  [ ] 不通过

备注:
_______________________
```

## 联系支持

如有问题，请查看：
- [README.md](./README.md) - 使用文档
- [设计文档](../../docs/plans/skills-system-design.md) - 技术细节
- [集成测试](./tests/integration_tests.rs) - 测试用例

---

**准备好了吗？** 运行 `skills-cli demo` 开始验收！
