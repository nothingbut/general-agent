// 当前会话ID
let currentSessionId = null;

// 发送消息
async function sendMessage() {
    const input = document.getElementById('input');
    const message = input.value.trim();

    if (!message) return;

    // 关闭之前的错误提示
    closeErrorBanner();

    // 显示用户消息
    appendMessage('user', message);

    // 清空输入框
    input.value = '';

    // 禁用发送按钮
    const sendBtn = document.getElementById('send-btn');
    sendBtn.disabled = true;
    sendBtn.textContent = '发送中...';

    // 更新状态为处理中
    updateStatus('处理中...', '#667eea');

    try {
        // 发送请求
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                message: message,
                session_id: currentSessionId
            })
        });

        // 检查HTTP状态
        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            const errorMsg = errorData.detail || `服务器错误 (${response.status})`;
            throw new Error(errorMsg);
        }

        const data = await response.json();

        // 保存session_id
        if (!currentSessionId) {
            currentSessionId = data.session_id;
        }

        // 检查响应是否为空
        if (!data.response || data.response.trim() === '') {
            showError('⚠️ AI返回了空响应，请稍后重试或检查模型配置');
            updateStatus('错误', '#dc3545');
            return;
        }

        // 检查是否有错误标志
        if (data.success === false) {
            const errorMsg = data.error || '未知错误';
            showError(`❌ AI处理失败: ${errorMsg}`);
            appendMessage('assistant', `抱歉，处理您的请求时出错了: ${errorMsg}`);
            updateStatus('错误', '#dc3545');
            return;
        }

        // 显示助手回复
        appendMessage('assistant', data.response);
        updateStatus('已连接', '#28a745');

    } catch (error) {
        console.error('Error:', error);

        // 区分不同类型的错误
        let errorMessage = '';
        if (error.name === 'TypeError' && error.message.includes('fetch')) {
            errorMessage = '❌ 无法连接到服务器，请检查服务器是否运行';
        } else if (error.message.includes('timeout') || error.message.includes('超时')) {
            errorMessage = '⏱️ 请求超时，可能是模型响应太慢。建议：\n1. 等待更长时间\n2. 或切换到更快的模型（如 llama3.2:3b）';
        } else if (error.message.includes('Ollama')) {
            errorMessage = `🤖 Ollama 错误: ${error.message}`;
        } else {
            errorMessage = `❌ 错误: ${error.message}`;
        }

        showError(errorMessage);
        appendMessage('assistant', '抱歉，发生了错误。请查看页面顶部的错误提示。');
        updateStatus('错误', '#dc3545');

    } finally {
        // 恢复发送按钮
        sendBtn.disabled = false;
        sendBtn.textContent = '发送';
    }
}

// 显示错误提示
function showError(message) {
    const banner = document.getElementById('error-banner');
    const errorMsg = document.getElementById('error-message');

    errorMsg.textContent = message;
    banner.classList.remove('hidden');

    // 5秒后自动隐藏（除非是超时错误）
    if (!message.includes('超时') && !message.includes('timeout')) {
        setTimeout(() => {
            closeErrorBanner();
        }, 10000);
    }
}

// 关闭错误提示
function closeErrorBanner() {
    const banner = document.getElementById('error-banner');
    banner.classList.add('hidden');
}

// 添加消息到界面
function appendMessage(role, content) {
    const messagesDiv = document.getElementById('messages');
    const messageDiv = document.createElement('div');
    messageDiv.className = `message ${role}`;

    // 创建消息内容容器
    const contentDiv = document.createElement('div');
    contentDiv.className = 'message-content';
    contentDiv.textContent = content;

    // 创建时间戳
    const timestamp = document.createElement('div');
    timestamp.className = 'message-timestamp';
    const now = new Date();
    timestamp.textContent = now.toLocaleTimeString('zh-CN', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });

    messageDiv.appendChild(contentDiv);
    messageDiv.appendChild(timestamp);
    messagesDiv.appendChild(messageDiv);

    // 滚动到底部
    messagesDiv.scrollTop = messagesDiv.scrollHeight;
}

// 更新状态
function updateStatus(text, color = '#28a745') {
    const status = document.getElementById('status');
    status.textContent = text;
    status.style.color = color;
}

// 监听Enter键发送
document.addEventListener('DOMContentLoaded', () => {
    const input = document.getElementById('input');

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });
});
