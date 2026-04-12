#!/bin/bash
# 快速验证测试脚本
# 使用方法: ./quick-test.sh

set -e

echo "=================================================="
echo "General Agent V3 快速验证测试"
echo "=================================================="
echo ""

# 颜色定义
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 1. 编译项目
echo "1. 编译项目..."
if dotnet build --configuration Release -v quiet > /dev/null 2>&1; then
    echo -e "${GREEN}✓ 编译成功${NC}"
else
    echo -e "${RED}✗ 编译失败${NC}"
    exit 1
fi
echo ""

# 2. 运行核心测试（不包括外部依赖）
echo "2. 运行核心测试..."
TEST_RESULT=$(dotnet test --no-build --verbosity quiet \
    --filter "FullyQualifiedName!~Qdrant&FullyQualifiedName!~Ollama" 2>&1 | \
    grep -E "失败|通过" | tail -1)

if echo "$TEST_RESULT" | grep -q "失败:     0"; then
    echo -e "${GREEN}✓ 所有核心测试通过${NC}"
    echo "$TEST_RESULT"
else
    echo -e "${RED}✗ 部分测试失败${NC}"
    echo "$TEST_RESULT"
    exit 1
fi
echo ""

# 3. 测试计划任务功能
echo "3. 测试计划任务功能..."

# 保存原始目录
ORIGINAL_DIR=$(pwd)
cd src/GeneralAgent.Hosts.Console

# 创建测试任务（使用 --no-build 避免重复构建）
TASK_OUTPUT=$(dotnet run --no-build -- task schedule "快速测试" \
    --schedule "每天9:00" \
    --type reminder \
    --payload '{"message":"测试"}' \
    --description "自动化测试任务" 2>&1)

if echo "$TASK_OUTPUT" | grep -q "任务创建成功"; then
    echo -e "${GREEN}✓ 任务创建成功${NC}"

    # 提取任务 ID（从输出中提取）
    TASK_ID=$(echo "$TASK_OUTPUT" | grep "任务 ID" | grep -oE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' | head -1)
    echo "  任务 ID: $TASK_ID"

    # 列出任务（使用 --no-build）
    TASK_LIST_OUTPUT=$(dotnet run --no-build -- task list 2>&1)
    if echo "$TASK_LIST_OUTPUT" | grep -q "$TASK_ID"; then
        echo -e "${GREEN}✓ 任务列表正常${NC}"
    else
        echo -e "${RED}✗ 任务列表失败${NC}"
        echo "任务列表输出:"
        echo "$TASK_LIST_OUTPUT"
        cd "$ORIGINAL_DIR"
        exit 1
    fi

    # 删除测试任务（使用 --no-build）
    DELETE_OUTPUT=$(dotnet run --no-build -- task delete "$TASK_ID" --force 2>&1)
    if echo "$DELETE_OUTPUT" | grep -q "任务已删除"; then
        echo -e "${GREEN}✓ 任务删除成功${NC}"
    else
        echo -e "${YELLOW}⚠ 任务删除失败（可手动删除）${NC}"
        echo "删除输出: $DELETE_OUTPUT"
    fi
else
    echo -e "${RED}✗ 任务创建失败${NC}"
    echo "创建任务输出:"
    echo "$TASK_OUTPUT"
    cd "$ORIGINAL_DIR"
    exit 1
fi

# 4. 测试基本命令
echo ""
echo "4. 测试基本命令..."
if dotnet run --no-build -- --help 2>&1 | grep -q "Description:"; then
    echo -e "${GREEN}✓ 帮助命令正常${NC}"
else
    echo -e "${RED}✗ 帮助命令失败${NC}"
    cd "$ORIGINAL_DIR"
    exit 1
fi

# 返回原始目录
cd "$ORIGINAL_DIR"
echo ""

# 总结
echo "=================================================="
echo -e "${GREEN}✓ 所有测试通过！${NC}"
echo "=================================================="
echo ""
echo "您现在可以："
echo "  1. 运行 REPL: dotnet run"
echo "  2. 查看帮助: dotnet run -- --help"
echo "  3. 管理任务: dotnet run -- task --help"
echo ""
echo "详细测试指南: v3/ACCEPTANCE_TEST_GUIDE.md"
