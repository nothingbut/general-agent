#!/bin/bash
# setup_ollama.sh - 一键配置 Ollama 本地模型

set -e

# 颜色定义
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}  General Agent - Ollama 配置工具${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# 检查操作系统
OS="$(uname -s)"
echo "检测到操作系统: $OS"
echo ""

# 步骤1：检查 Ollama 是否安装
echo -e "${BLUE}[1/5]${NC} 检查 Ollama 安装状态..."

if command -v ollama &> /dev/null; then
    OLLAMA_VERSION=$(ollama --version 2>&1 | head -n 1)
    echo -e "${GREEN}✅ Ollama 已安装: $OLLAMA_VERSION${NC}"
else
    echo -e "${YELLOW}⚠️  Ollama 未安装${NC}"
    echo ""
    echo "请选择安装方式:"
    echo "  1) 自动安装（推荐）"
    echo "  2) 手动安装"
    echo "  3) 跳过安装"
    read -p "请选择 [1-3]: " choice

    case $choice in
        1)
            echo "正在安装 Ollama..."
            if [ "$OS" = "Darwin" ]; then
                # macOS
                if command -v brew &> /dev/null; then
                    brew install ollama
                else
                    echo -e "${YELLOW}未检测到 Homebrew，请手动安装: https://ollama.ai/download${NC}"
                    exit 1
                fi
            elif [ "$OS" = "Linux" ]; then
                # Linux
                curl -fsSL https://ollama.ai/install.sh | sh
            else
                echo -e "${RED}不支持的操作系统，请手动安装: https://ollama.ai/download${NC}"
                exit 1
            fi
            ;;
        2)
            echo -e "${YELLOW}请访问 https://ollama.ai/download 手动安装 Ollama${NC}"
            exit 0
            ;;
        3)
            echo -e "${YELLOW}跳过安装，退出配置${NC}"
            exit 0
            ;;
        *)
            echo -e "${RED}无效选择${NC}"
            exit 1
            ;;
    esac
fi

echo ""

# 步骤2：启动 Ollama 服务
echo -e "${BLUE}[2/5]${NC} 检查 Ollama 服务状态..."

if curl -s http://localhost:11434 > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Ollama 服务正在运行${NC}"
else
    echo -e "${YELLOW}⚠️  Ollama 服务未运行，正在启动...${NC}"

    # 尝试启动服务
    if [ "$OS" = "Darwin" ]; then
        # macOS - 后台启动
        nohup ollama serve > /tmp/ollama.log 2>&1 &
        sleep 3
    elif [ "$OS" = "Linux" ]; then
        # Linux - 使用 systemd 或后台启动
        if systemctl is-active --quiet ollama 2>/dev/null; then
            sudo systemctl start ollama
        else
            nohup ollama serve > /tmp/ollama.log 2>&1 &
        fi
        sleep 3
    fi

    # 验证启动
    if curl -s http://localhost:11434 > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Ollama 服务启动成功${NC}"
    else
        echo -e "${RED}❌ Ollama 服务启动失败，请手动运行: ollama serve${NC}"
        exit 1
    fi
fi

echo ""

# 步骤3：选择和下载模型
echo -e "${BLUE}[3/5]${NC} 选择 LLM 模型..."

# 检查已有模型
EXISTING_MODELS=$(ollama list 2>/dev/null | tail -n +2 | awk '{print $1}' | grep -v "^$" || echo "")

if [ -n "$EXISTING_MODELS" ]; then
    echo -e "${GREEN}已安装的模型:${NC}"
    echo "$EXISTING_MODELS" | nl
    echo ""
fi

echo "推荐模型:"
echo "  1) llama3.2:3b (推荐，~2GB，4GB+ 内存)"
echo "  2) llama3.2:1b (轻量，~1GB，2GB+ 内存)"
echo "  3) llama3.1:8b (高质量，~4.7GB，8GB+ 内存)"
echo "  4) qwen2.5:latest (中文优化，~4.7GB，8GB+ 内存)"
echo "  5) 使用已有模型"
echo ""

read -p "请选择模型 [1-5]: " model_choice

case $model_choice in
    1)
        MODEL_NAME="llama3.2:3b"
        ;;
    2)
        MODEL_NAME="llama3.2:1b"
        ;;
    3)
        MODEL_NAME="llama3.1:8b"
        ;;
    4)
        MODEL_NAME="qwen2.5:latest"
        ;;
    5)
        if [ -n "$EXISTING_MODELS" ]; then
            echo "请输入模型名称（从上面列表选择）:"
            read MODEL_NAME
        else
            echo -e "${RED}没有已安装的模型${NC}"
            exit 1
        fi
        ;;
    *)
        echo -e "${RED}无效选择，使用默认模型: llama3.2:3b${NC}"
        MODEL_NAME="llama3.2:3b"
        ;;
esac

echo ""
echo "选择的模型: $MODEL_NAME"

# 检查模型是否已下载
if ollama list | grep -q "$MODEL_NAME"; then
    echo -e "${GREEN}✅ 模型已存在: $MODEL_NAME${NC}"
else
    echo -e "${YELLOW}正在下载模型: $MODEL_NAME (这可能需要几分钟)...${NC}"
    if ollama pull "$MODEL_NAME"; then
        echo -e "${GREEN}✅ 模型下载成功${NC}"
    else
        echo -e "${RED}❌ 模型下载失败${NC}"
        exit 1
    fi
fi

echo ""

# 步骤4：创建配置文件
echo -e "${BLUE}[4/5]${NC} 创建配置文件..."

if [ -f ".env" ]; then
    echo -e "${YELLOW}⚠️  .env 文件已存在${NC}"
    read -p "是否覆盖? [y/N]: " overwrite
    if [ "$overwrite" != "y" ] && [ "$overwrite" != "Y" ]; then
        echo "保留现有配置"
    else
        # 备份现有配置
        cp .env .env.backup
        echo -e "${GREEN}已备份为 .env.backup${NC}"

        # 创建新配置
        cat > .env << EOF
# General Agent 环境配置
# 生成时间: $(date)

# ================================
# LLM 配置
# ================================

USE_OLLAMA=true
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=$MODEL_NAME
OLLAMA_TEMPERATURE=0.7

# ================================
# 数据库配置
# ================================

DATABASE_PATH=data/general_agent.db

# ================================
# 服务器配置
# ================================

PORT=8000
LOG_LEVEL=INFO
EOF
        echo -e "${GREEN}✅ 配置文件已创建: .env${NC}"
    fi
else
    # 创建新配置
    cat > .env << EOF
# General Agent 环境配置
# 生成时间: $(date)

# ================================
# LLM 配置
# ================================

USE_OLLAMA=true
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=$MODEL_NAME
OLLAMA_TEMPERATURE=0.7

# ================================
# 数据库配置
# ================================

DATABASE_PATH=data/general_agent.db

# ================================
# 服务器配置
# ================================

PORT=8000
LOG_LEVEL=INFO
EOF
    echo -e "${GREEN}✅ 配置文件已创建: .env${NC}"
fi

echo ""

# 步骤5：测试配置
echo -e "${BLUE}[5/5]${NC} 测试 Ollama 连接..."

# 简单测试
TEST_RESPONSE=$(curl -s -X POST http://localhost:11434/api/generate \
    -H "Content-Type: application/json" \
    -d "{\"model\": \"$MODEL_NAME\", \"prompt\": \"Say hello\", \"stream\": false}" \
    2>/dev/null)

if echo "$TEST_RESPONSE" | grep -q "response"; then
    echo -e "${GREEN}✅ Ollama 配置成功！${NC}"
else
    echo -e "${YELLOW}⚠️  测试连接失败，但配置已完成${NC}"
    echo "请手动测试: ollama run $MODEL_NAME"
fi

echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}  🎉 配置完成！${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "下一步："
echo "  1. 启动 General Agent:"
echo "     uvicorn src.main:app --reload --port 8000"
echo ""
echo "  2. 访问 Web 界面:"
echo "     http://localhost:8000"
echo ""
echo "  3. 测试 API:"
echo "     curl -X POST http://localhost:8000/api/chat \\"
echo "       -H 'Content-Type: application/json' \\"
echo "       -d '{\"message\": \"Hello!\", \"session_id\": \"test\"}'"
echo ""
echo "配置文件: .env"
echo "使用的模型: $MODEL_NAME"
echo "详细文档: docs/OLLAMA_SETUP.md"
echo ""
