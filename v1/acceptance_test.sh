#!/bin/bash
# acceptance_test.sh - Phase 1&2 快速验收脚本

set -e

echo "🚀 开始 Phase 1 & 2 验收测试..."
echo ""

# 颜色定义
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 计数器
PASSED=0
FAILED=0
WARNED=0

# 检查服务器是否运行
echo "📡 检查服务器状态..."
if curl -s http://localhost:8000/health > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 服务器运行中${NC}"
else
    echo -e "${RED}❌ 服务器未运行${NC}"
    echo -e "${YELLOW}请先启动服务器: uvicorn src.main:app --reload${NC}"
    exit 1
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🧪 Phase 1: 基础框架测试"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# 测试1：基本聊天
echo -n "1. 测试基本聊天功能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Hello", "session_id": "acceptance-test-1"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试2：会话ID自动生成
echo -n "2. 测试会话ID自动生成... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Test"}')

if echo $RESPONSE | jq -e '.session_id' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试3：上下文管理
echo -n "3. 测试上下文管理... "
curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "My name is TestUser", "session_id": "context-test"}' > /dev/null

sleep 1

RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What is my name?", "session_id": "context-test"}')

if echo $RESPONSE | grep -iq "testuser"; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${YELLOW}⚠️  警告（AI可能未记住名字）${NC}"
    ((WARNED++))
fi

# 测试4：数据持久化
echo -n "4. 测试SQLite数据持久化... "
if [ -f "data/agent.db" ]; then
    MESSAGE_COUNT=$(sqlite3 data/agent.db "SELECT COUNT(*) FROM messages;" 2>/dev/null || echo "0")
    if [ "$MESSAGE_COUNT" -gt 0 ]; then
        echo -e "${GREEN}✅ 通过 (${MESSAGE_COUNT}条消息)${NC}"
        ((PASSED++))
    else
        echo -e "${RED}❌ 失败 (无消息记录)${NC}"
        ((FAILED++))
    fi
else
    echo -e "${RED}❌ 失败 (数据库文件不存在)${NC}"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🎯 Phase 2: 技能系统测试"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# 测试5：greeting技能
echo -n "5. 测试 greeting 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@greeting", "session_id": "skill-test-1"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试6：reminder技能（带参数）
echo -n "6. 测试 reminder 技能 (带参数)... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@reminder task=\"Meeting\" time=\"5pm\"", "session_id": "skill-test-2"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    if echo $RESPONSE | grep -iq "meeting" && echo $RESPONSE | grep -iq "5pm"; then
        echo -e "${GREEN}✅ 通过 (参数正确传递)${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  警告 (参数可能未正确处理)${NC}"
        ((WARNED++))
    fi
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试7：note技能
echo -n "7. 测试 note 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@note content=\"Test note\" category=\"test\"", "session_id": "skill-test-3"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试8：task技能
echo -n "8. 测试 task 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@task title=\"Review\" priority=\"high\"", "session_id": "skill-test-4"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试9：brainstorm技能
echo -n "9. 测试 brainstorm 技能... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@brainstorm topic=\"Features\"", "session_id": "skill-test-5"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 通过${NC}"
    ((PASSED++))
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

# 测试10：错误处理
echo -n "10. 测试错误处理 (不存在的技能)... "
RESPONSE=$(curl -s -X POST http://localhost:8000/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "@nonexistent", "session_id": "error-test"}')

if echo $RESPONSE | jq -e '.response' > /dev/null 2>&1; then
    if echo $RESPONSE | grep -iq "not found\|unknown\|不存在"; then
        echo -e "${GREEN}✅ 通过 (友好错误提示)${NC}"
        ((PASSED++))
    else
        echo -e "${YELLOW}⚠️  警告 (错误提示可能不够清晰)${NC}"
        ((WARNED++))
    fi
else
    echo -e "${RED}❌ 失败${NC}"
    ((FAILED++))
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📊 测试套件执行"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

echo "运行 pytest 测试套件..."
if pytest tests/ -v --tb=short --maxfail=3 2>&1 | tail -20; then
    echo -e "${GREEN}✅ 测试套件通过${NC}"
else
    echo -e "${YELLOW}⚠️  部分测试失败 (查看详细日志)${NC}"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "📈 验收测试结果汇总"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo -e "${GREEN}✅ 通过: $PASSED${NC}"
echo -e "${YELLOW}⚠️  警告: $WARNED${NC}"
echo -e "${RED}❌ 失败: $FAILED${NC}"
echo ""

TOTAL=$((PASSED + WARNED + FAILED))
SUCCESS_RATE=$((PASSED * 100 / TOTAL))

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${GREEN}🎉 验收测试通过！成功率: ${SUCCESS_RATE}%${NC}"
    echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
else
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${RED}❌ 验收测试失败，成功率: ${SUCCESS_RATE}%${NC}"
    echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
fi

echo ""
echo "📝 下一步："
echo "  1. 查看详细验收文档: docs/ACCEPTANCE_TEST.md"
echo "  2. 手动验证Web界面: http://localhost:8000"
echo "  3. 检查API文档: http://localhost:8000/docs"
echo "  4. 记录发现的问题到验收清单"
echo ""

exit $FAILED
