"""工作流通知系统 - 多渠道通知管理"""
import asyncio
import logging
import uuid
import subprocess
import platform
from abc import ABC, abstractmethod
from datetime import datetime
from typing import Optional, Dict, List, Any
from enum import Enum
from dataclasses import dataclass, field

from rich.console import Console
from rich.panel import Panel
from rich.text import Text

logger = logging.getLogger(__name__)


class NotificationPriority(Enum):
    """通知优先级"""
    CRITICAL = "critical"
    HIGH = "high"
    NORMAL = "normal"
    LOW = "low"

    @classmethod
    def from_tool(cls, tool_name: str) -> "NotificationPriority":
        """根据工具名称推断优先级"""
        tool_lower = tool_name.lower()
        if any(word in tool_lower for word in ['delete', 'remove', 'drop', 'execute', 'run', 'shell']):
            return cls.CRITICAL
        if any(word in tool_lower for word in ['write', 'update', 'create', 'modify']):
            return cls.HIGH
        if any(word in tool_lower for word in ['read', 'list', 'get', 'search']):
            return cls.NORMAL
        return cls.NORMAL


@dataclass
class Notification:
    """通知模型"""
    id: str = field(default_factory=lambda: str(uuid.uuid4()))
    workflow_id: str = ""
    task_id: str = ""
    title: str = ""
    message: str = ""
    priority: NotificationPriority = NotificationPriority.NORMAL
    channels: List[str] = field(default_factory=lambda: ["terminal"])
    created_at: datetime = field(default_factory=datetime.now)
    read: bool = False
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        """转换为字典"""
        return {
            "id": self.id,
            "workflow_id": self.workflow_id,
            "task_id": self.task_id,
            "title": self.title,
            "message": self.message,
            "priority": self.priority.value,
            "channels": self.channels,
            "created_at": self.created_at.isoformat() if self.created_at else None,
            "read": self.read,
            "metadata": self.metadata
        }


class NotificationChannel(ABC):
    """通知渠道抽象基类"""

    @abstractmethod
    async def send(self, notification: Notification) -> bool:
        """发送通知"""
        pass

    @abstractmethod
    def is_available(self) -> bool:
        """检查渠道是否可用"""
        pass


class TerminalChannel(NotificationChannel):
    """终端通知渠道"""

    def __init__(self, console: Optional[Console] = None):
        self.console = console or Console()
        self._priority_styles = {
            NotificationPriority.CRITICAL: ("red", "🔴"),
            NotificationPriority.HIGH: ("yellow", "🟡"),
            NotificationPriority.NORMAL: ("cyan", "🔵"),
            NotificationPriority.LOW: ("dim", "⚪")
        }

    async def send(self, notification: Notification) -> bool:
        try:
            style, icon = self._priority_styles.get(
                notification.priority,
                ("white", "⚪")
            )
            title_text = Text()
            title_text.append(f"{icon} ", style=f"bold {style}")
            title_text.append(notification.title, style=f"bold {style}")
            panel = Panel(
                notification.message,
                title=title_text,
                border_style=style,
                padding=(1, 2)
            )
            self.console.print()
            self.console.print(panel)
            self.console.print()
            return True
        except Exception as e:
            logger.error(f"Failed to send terminal notification: {e}")
            return False

    def is_available(self) -> bool:
        return True


class DesktopChannel(NotificationChannel):
    """桌面通知渠道"""

    def __init__(self):
        self.system = platform.system()

    async def send(self, notification: Notification) -> bool:
        try:
            if self.system == "Darwin":
                return await self._send_macos(notification)
            elif self.system == "Linux":
                return await self._send_linux(notification)
            elif self.system == "Windows":
                return await self._send_windows(notification)
            else:
                logger.warning(f"Desktop notifications not supported on {self.system}")
                return False
        except Exception as e:
            logger.error(f"Failed to send desktop notification: {e}")
            return False

    async def _send_macos(self, notification: Notification) -> bool:
        try:
            script = f'display notification "{notification.message}" with title "{notification.title}" sound name "default"'
            process = await asyncio.create_subprocess_exec(
                "osascript", "-e", script,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            if process.returncode == 0:
                logger.debug("macOS notification sent successfully")
                return True
            else:
                logger.error(f"macOS notification failed: {stderr.decode()}")
                return False
        except Exception as e:
            logger.error(f"macOS notification error: {e}")
            return False

    async def _send_linux(self, notification: Notification) -> bool:
        try:
            urgency_map = {
                NotificationPriority.CRITICAL: "critical",
                NotificationPriority.HIGH: "normal",
                NotificationPriority.NORMAL: "normal",
                NotificationPriority.LOW: "low"
            }
            urgency = urgency_map.get(notification.priority, "normal")
            process = await asyncio.create_subprocess_exec(
                "notify-send", "-u", urgency,
                notification.title, notification.message,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            return process.returncode == 0
        except FileNotFoundError:
            logger.error("notify-send not found")
            return False
        except Exception as e:
            logger.error(f"Linux notification error: {e}")
            return False

    async def _send_windows(self, notification: Notification) -> bool:
        try:
            script = f'''
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
$template = @"
<toast><visual><binding template="ToastText02">
<text id="1">{notification.title}</text>
<text id="2">{notification.message}</text>
</binding></visual></toast>
"@
$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = New-Object Windows.UI.Notifications.ToastNotification $xml
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier("General Agent").Show($toast)
'''
            process = await asyncio.create_subprocess_exec(
                "powershell", "-Command", script,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE
            )
            stdout, stderr = await process.communicate()
            return process.returncode == 0
        except Exception as e:
            logger.error(f"Windows notification error: {e}")
            return False

    def is_available(self) -> bool:
        if self.system == "Darwin":
            return True
        elif self.system == "Linux":
            try:
                subprocess.run(["which", "notify-send"], capture_output=True, check=True)
                return True
            except subprocess.CalledProcessError:
                return False
        elif self.system == "Windows":
            return True
        return False


class NotificationManager:
    """通知管理器"""

    def __init__(self, database=None):
        self.database = database
        self._channels: Dict[str, NotificationChannel] = {}
        self.register_channel("terminal", TerminalChannel())

    def register_channel(self, name: str, channel: NotificationChannel) -> None:
        if not channel.is_available():
            logger.warning(f"Channel '{name}' is not available")
            return
        self._channels[name] = channel
        logger.info(f"Registered notification channel: {name}")

    def unregister_channel(self, name: str) -> None:
        if name in self._channels:
            del self._channels[name]

    async def send(self, notification: Notification) -> Dict[str, bool]:
        results = {}
        for channel_name in notification.channels:
            if channel_name not in self._channels:
                results[channel_name] = False
                continue
            channel = self._channels[channel_name]
            success = await channel.send(notification)
            results[channel_name] = success
        return results

    def get_available_channels(self) -> List[str]:
        return list(self._channels.keys())
