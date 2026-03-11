"""通知系统测试"""
import pytest
import asyncio
from unittest.mock import Mock, patch, AsyncMock
from io import StringIO
from datetime import datetime

from rich.console import Console

from src.workflow.notification import (
    Notification,
    NotificationPriority,
    NotificationChannel,
    TerminalChannel,
    DesktopChannel,
    NotificationManager
)


class TestNotificationPriority:
    """测试通知优先级"""

    def test_priority_values(self):
        """测试优先级枚举值"""
        assert NotificationPriority.CRITICAL.value == "critical"
        assert NotificationPriority.HIGH.value == "high"
        assert NotificationPriority.NORMAL.value == "normal"
        assert NotificationPriority.LOW.value == "low"

    def test_from_tool_critical(self):
        """测试从工具名推断关键优先级"""
        assert NotificationPriority.from_tool("mcp:filesystem:delete") == NotificationPriority.CRITICAL
        assert NotificationPriority.from_tool("mcp:shell:execute") == NotificationPriority.CRITICAL
        assert NotificationPriority.from_tool("test:remove") == NotificationPriority.CRITICAL

    def test_from_tool_high(self):
        """测试从工具名推断高优先级"""
        assert NotificationPriority.from_tool("mcp:filesystem:write") == NotificationPriority.HIGH
        assert NotificationPriority.from_tool("test:update") == NotificationPriority.HIGH
        assert NotificationPriority.from_tool("test:create") == NotificationPriority.HIGH

    def test_from_tool_normal(self):
        """测试从工具名推断普通优先级"""
        assert NotificationPriority.from_tool("mcp:filesystem:read") == NotificationPriority.NORMAL
        assert NotificationPriority.from_tool("test:list") == NotificationPriority.NORMAL
        assert NotificationPriority.from_tool("test:get") == NotificationPriority.NORMAL

    def test_from_tool_default(self):
        """测试未知工具的默认优先级"""
        assert NotificationPriority.from_tool("unknown:tool") == NotificationPriority.NORMAL


class TestNotification:
    """测试通知模型"""

    def test_create_notification(self):
        """测试创建通知"""
        notif = Notification(
            workflow_id="workflow-123",
            task_id="task-1",
            title="测试通知",
            message="这是测试消息"
        )

        assert notif.id is not None
        assert notif.workflow_id == "workflow-123"
        assert notif.task_id == "task-1"
        assert notif.title == "测试通知"
        assert notif.message == "这是测试消息"
        assert notif.priority == NotificationPriority.NORMAL
        assert notif.channels == ["terminal"]
        assert not notif.read

    def test_notification_with_priority(self):
        """测试带优先级的通知"""
        notif = Notification(
            title="Critical",
            message="Critical message",
            priority=NotificationPriority.CRITICAL
        )

        assert notif.priority == NotificationPriority.CRITICAL

    def test_notification_with_channels(self):
        """测试指定通知渠道"""
        notif = Notification(
            title="Test",
            message="Message",
            channels=["terminal", "desktop"]
        )

        assert notif.channels == ["terminal", "desktop"]

    def test_notification_to_dict(self):
        """测试通知转字典"""
        notif = Notification(
            workflow_id="wf-1",
            task_id="task-1",
            title="Test",
            message="Message",
            priority=NotificationPriority.HIGH
        )

        data = notif.to_dict()

        assert data["workflow_id"] == "wf-1"
        assert data["task_id"] == "task-1"
        assert data["title"] == "Test"
        assert data["message"] == "Message"
        assert data["priority"] == "high"
        assert data["channels"] == ["terminal"]
        assert data["read"] is False


class TestTerminalChannel:
    """测试终端通知渠道"""

    @pytest.mark.asyncio
    async def test_send_notification(self):
        """测试发送终端通知"""
        console = Console(file=StringIO())
        channel = TerminalChannel(console=console)

        notif = Notification(
            title="测试通知",
            message="这是测试消息"
        )

        result = await channel.send(notif)

        assert result is True

    @pytest.mark.asyncio
    async def test_send_critical_notification(self):
        """测试发送关键通知"""
        console = Console(file=StringIO())
        channel = TerminalChannel(console=console)

        notif = Notification(
            title="关键通知",
            message="删除操作",
            priority=NotificationPriority.CRITICAL
        )

        result = await channel.send(notif)

        assert result is True

    @pytest.mark.asyncio
    async def test_send_notification_error(self):
        """测试通知发送错误处理"""
        # Mock console to raise exception
        console = Mock()
        console.print = Mock(side_effect=Exception("Test error"))

        channel = TerminalChannel(console=console)

        notif = Notification(title="Test", message="Message")

        result = await channel.send(notif)

        assert result is False

    def test_is_available(self):
        """测试终端渠道总是可用"""
        channel = TerminalChannel()
        assert channel.is_available() is True


class TestDesktopChannel:
    """测试桌面通知渠道"""

    def test_initialization(self):
        """测试初始化"""
        channel = DesktopChannel()
        assert channel.system in ["Darwin", "Linux", "Windows", ""]

    @pytest.mark.asyncio
    async def test_send_macos_notification(self):
        """测试 macOS 通知"""
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Darwin"):
            notif = Notification(title="Test", message="Message")

            # Mock subprocess
            with patch('asyncio.create_subprocess_exec') as mock_exec:
                mock_process = AsyncMock()
                mock_process.communicate = AsyncMock(return_value=(b"", b""))
                mock_process.returncode = 0
                mock_exec.return_value = mock_process

                result = await channel.send(notif)

                assert result is True
                mock_exec.assert_called_once()

    @pytest.mark.asyncio
    async def test_send_linux_notification(self):
        """测试 Linux 通知"""
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Linux"):
            notif = Notification(title="Test", message="Message")

            # Mock subprocess
            with patch('asyncio.create_subprocess_exec') as mock_exec:
                mock_process = AsyncMock()
                mock_process.communicate = AsyncMock(return_value=(b"", b""))
                mock_process.returncode = 0
                mock_exec.return_value = mock_process

                result = await channel.send(notif)

                assert result is True

    @pytest.mark.asyncio
    async def test_send_windows_notification(self):
        """测试 Windows 通知"""
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Windows"):
            notif = Notification(title="Test", message="Message")

            # Mock subprocess
            with patch('asyncio.create_subprocess_exec') as mock_exec:
                mock_process = AsyncMock()
                mock_process.communicate = AsyncMock(return_value=(b"", b""))
                mock_process.returncode = 0
                mock_exec.return_value = mock_process

                result = await channel.send(notif)

                assert result is True

    @pytest.mark.asyncio
    async def test_send_unsupported_system(self):
        """测试不支持的系统"""
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Unknown"):
            notif = Notification(title="Test", message="Message")
            result = await channel.send(notif)
            assert result is False

    def test_is_available_macos(self):
        """测试 macOS 可用性"""
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Darwin"):
            assert channel.is_available() is True

    def test_is_available_linux_with_notify_send(self):
        """测试 Linux 可用性（有 notify-send）"""
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Linux"):
            with patch('subprocess.run') as mock_run:
                mock_run.return_value = Mock(returncode=0)
                assert channel.is_available() is True

    def test_is_available_linux_without_notify_send(self):
        """测试 Linux 可用性（无 notify-send）"""
        import subprocess
        channel = DesktopChannel()

        with patch.object(channel, 'system', "Linux"):
            with patch('subprocess.run', side_effect=subprocess.CalledProcessError(1, "which")):
                assert channel.is_available() is False


class TestNotificationManager:
    """测试通知管理器"""

    def test_initialization(self):
        """测试初始化"""
        manager = NotificationManager()
        assert manager.database is None
        assert len(manager._channels) == 1  # 默认注册 terminal

    def test_initialization_with_database(self):
        """测试带数据库初始化"""
        db = Mock()
        manager = NotificationManager(database=db)
        assert manager.database is db

    def test_register_channel(self):
        """测试注册渠道"""
        manager = NotificationManager()

        channel = Mock(spec=NotificationChannel)
        channel.is_available = Mock(return_value=True)

        manager.register_channel("test", channel)

        assert "test" in manager._channels
        assert manager._channels["test"] is channel

    def test_register_unavailable_channel(self):
        """测试注册不可用的渠道"""
        manager = NotificationManager()

        channel = Mock(spec=NotificationChannel)
        channel.is_available = Mock(return_value=False)

        manager.register_channel("test", channel)

        assert "test" not in manager._channels

    def test_unregister_channel(self):
        """测试取消注册渠道"""
        manager = NotificationManager()

        channel = Mock(spec=NotificationChannel)
        channel.is_available = Mock(return_value=True)

        manager.register_channel("test", channel)
        assert "test" in manager._channels

        manager.unregister_channel("test")
        assert "test" not in manager._channels

    @pytest.mark.asyncio
    async def test_send_notification_single_channel(self):
        """测试发送通知到单个渠道"""
        manager = NotificationManager()

        notif = Notification(
            title="Test",
            message="Message",
            channels=["terminal"]
        )

        results = await manager.send(notif)

        assert "terminal" in results
        assert results["terminal"] is True

    @pytest.mark.asyncio
    async def test_send_notification_multiple_channels(self):
        """测试发送通知到多个渠道"""
        manager = NotificationManager()

        # 注册 mock 渠道
        channel1 = Mock(spec=NotificationChannel)
        channel1.is_available = Mock(return_value=True)
        channel1.send = AsyncMock(return_value=True)

        channel2 = Mock(spec=NotificationChannel)
        channel2.is_available = Mock(return_value=True)
        channel2.send = AsyncMock(return_value=True)

        manager.register_channel("channel1", channel1)
        manager.register_channel("channel2", channel2)

        notif = Notification(
            title="Test",
            message="Message",
            channels=["terminal", "channel1", "channel2"]
        )

        results = await manager.send(notif)

        assert len(results) == 3
        assert results["terminal"] is True
        assert results["channel1"] is True
        assert results["channel2"] is True

    @pytest.mark.asyncio
    async def test_send_to_unregistered_channel(self):
        """测试发送到未注册的渠道"""
        manager = NotificationManager()

        notif = Notification(
            title="Test",
            message="Message",
            channels=["nonexistent"]
        )

        results = await manager.send(notif)

        assert "nonexistent" in results
        assert results["nonexistent"] is False

    def test_get_available_channels(self):
        """测试获取可用渠道"""
        manager = NotificationManager()

        channels = manager.get_available_channels()

        assert "terminal" in channels
        assert len(channels) == 1

    def test_get_available_channels_multiple(self):
        """测试获取多个可用渠道"""
        manager = NotificationManager()

        # 注册额外渠道
        channel = Mock(spec=NotificationChannel)
        channel.is_available = Mock(return_value=True)
        manager.register_channel("test", channel)

        channels = manager.get_available_channels()

        assert "terminal" in channels
        assert "test" in channels
        assert len(channels) == 2


class TestIntegration:
    """集成测试"""

    @pytest.mark.asyncio
    async def test_full_notification_flow(self):
        """测试完整通知流程"""
        manager = NotificationManager()

        # 创建通知
        notif = Notification(
            workflow_id="wf-123",
            task_id="task-1",
            title="审批请求",
            message="任务需要审批",
            priority=NotificationPriority.CRITICAL,
            channels=["terminal"]
        )

        # 发送通知
        results = await manager.send(notif)

        # 验证结果
        assert "terminal" in results
        assert results["terminal"] is True

    @pytest.mark.asyncio
    async def test_notification_with_priority_inference(self):
        """测试优先级推断"""
        priority = NotificationPriority.from_tool("mcp:filesystem:delete")

        notif = Notification(
            title="删除文件",
            message="即将删除文件",
            priority=priority
        )

        assert notif.priority == NotificationPriority.CRITICAL
