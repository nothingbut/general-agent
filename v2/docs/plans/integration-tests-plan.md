# 集成测试计划

**日期:** 2026-03-10
**目标:** 完整的端到端测试覆盖

---

## 🎯 测试目标

1. **验证全流程** - CLI → Workflow → LLM → Storage
2. **确保集成点正常** - 所有模块协同工作
3. **错误场景覆盖** - 异常情况正确处理
4. **并发安全** - 多会话、多消息并发场景

---

## 📋 测试清单

### 1. CLI 集成测试

**文件:** `v2/crates/agent-cli/tests/cli_integration_test.rs`

**测试用例:**
- [ ] `test_cli_new_session` - 创建会话
- [ ] `test_cli_list_sessions` - 列出会话
- [ ] `test_cli_chat_basic` - 基本对话
- [ ] `test_cli_chat_stream` - 流式对话
- [ ] `test_cli_delete_session` - 删除会话
- [ ] `test_cli_search_sessions` - 搜索会话
- [ ] `test_cli_with_skills` - 带技能的对话
- [ ] `test_cli_invalid_session_id` - 无效会话 ID
- [ ] `test_cli_provider_switch` - 提供商切换

---

### 2. 对话流程端到端测试

**文件:** `v2/crates/agent-workflow/tests/e2e_conversation_test.rs`

**测试用例:**
- [ ] `test_full_conversation_flow` - 完整对话流程
- [ ] `test_context_management` - 上下文管理
- [ ] `test_multi_turn_conversation` - 多轮对话
- [ ] `test_conversation_with_skills` - 技能调用
- [ ] `test_conversation_persistence` - 对话持久化
- [ ] `test_stream_conversation` - 流式对话
- [ ] `test_conversation_error_recovery` - 错误恢复

---

### 3. 技能系统集成测试扩展

**文件:** `v2/crates/agent-workflow/tests/skills_integration_test.rs` (已存在)

**新增测试用例:**
- [ ] `test_skill_parameter_validation` - 参数验证
- [ ] `test_skill_error_handling` - 错误处理
- [ ] `test_multiple_skills_in_conversation` - 多技能调用
- [ ] `test_skill_namespace_conflict` - 命名空间冲突
- [ ] `test_skill_with_default_values` - 默认值处理

---

### 4. LLM 集成测试

**文件:** `v2/crates/agent-llm/tests/llm_integration_test.rs`

**测试用例:**
- [ ] `test_anthropic_client_complete` - Anthropic 完成
- [ ] `test_anthropic_client_stream` - Anthropic 流式
- [ ] `test_ollama_client_complete` - Ollama 完成
- [ ] `test_ollama_client_stream` - Ollama 流式
- [ ] `test_llm_error_handling` - 错误处理
- [ ] `test_llm_timeout` - 超时处理
- [ ] `test_llm_token_usage` - Token 统计

---

### 5. 并发安全测试

**文件:** `v2/tests/concurrency_test.rs`

**测试用例:**
- [ ] `test_concurrent_sessions` - 并发会话创建
- [ ] `test_concurrent_messages` - 并发消息发送
- [ ] `test_concurrent_llm_calls` - 并发 LLM 调用
- [ ] `test_concurrent_skill_execution` - 并发技能执行
- [ ] `test_race_conditions` - 竞态条件

---

### 6. 存储层集成测试

**文件:** `v2/crates/agent-storage/tests/storage_integration_test.rs`

**测试用例:**
- [ ] `test_database_migrations` - 数据库迁移
- [ ] `test_transaction_rollback` - 事务回滚
- [ ] `test_concurrent_writes` - 并发写入
- [ ] `test_large_message_storage` - 大消息存储
- [ ] `test_session_cleanup` - 会话清理

---

## 🔧 测试工具

### Mock LLM Client
```rust
struct MockLLMClient {
    responses: Vec<String>,
    current: AtomicUsize,
}
```

### Test Helpers
```rust
// 创建测试环境
async fn setup_test_env() -> TestEnvironment

// 清理测试数据
async fn cleanup_test_env(env: TestEnvironment)

// 断言对话状态
fn assert_conversation_state(...)
```

---

## 📊 验收标准

- [ ] 所有测试通过
- [ ] 测试覆盖率 > 80%
- [ ] 无内存泄漏（使用 valgrind/miri 验证）
- [ ] 并发场景无死锁
- [ ] 所有错误路径测试

---

## 🚀 实施顺序

1. CLI 集成测试（基础）
2. 对话流程端到端测试（核心）
3. 技能系统扩展测试
4. LLM 集成测试
5. 并发安全测试
6. 存储层集成测试

---

## 📝 测试报告

每个测试完成后记录：
- 测试数量
- 通过率
- 发现的问题
- 修复的问题
- 性能指标

---

**状态:** 计划完成，开始实施
**预计时间:** 2-3 小时
