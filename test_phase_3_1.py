#!/usr/bin/env python
"""Phase 3.1 验收测试脚本"""

import sys
from pathlib import Path

# 添加 src 到 Python 路径
sys.path.insert(0, str(Path(__file__).parent))

def test_imports():
    """测试模块导入"""
    print("📦 测试模块导入...")
    try:
        from src.mcp import MCPConfig, MCPConnectionManager, ServerConfig, SecurityConfig
        print("   ✅ Phase 3.1 模块可以导入")
        print("   - MCPConfig")
        print("   - ServerConfig")
        print("   - SecurityConfig")
        print("   - MCPConnectionManager")
        return True
    except ImportError as e:
        print(f"   ❌ 导入失败: {e}")
        return False


def test_config_loading():
    """测试配置加载"""
    print("\n📄 测试配置加载...")
    try:
        from src.mcp.config import load_mcp_config
        config = load_mcp_config('config/mcp_config.yaml')
        
        print(f"   ✅ 配置加载成功")
        print(f"   - 服务器数量: {len(config.servers)}")
        print(f"   - 服务器列表: {list(config.servers.keys())}")
        print(f"   - 全局配置: enabled={config.global_config.get('enabled')}")
        
        # 检查 filesystem 服务器配置
        fs_server = config.servers.get('filesystem')
        if fs_server:
            print(f"   - filesystem 命令: {fs_server.command}")
            print(f"   - 允许的目录数: {len(fs_server.security.allowed_directories)}")
            print(f"   - 允许的操作: {fs_server.security.allowed_operations}")
        
        return True
    except Exception as e:
        print(f"   ❌ 配置加载失败: {e}")
        return False


def test_connection_manager():
    """测试连接管理器初始化"""
    print("\n🔌 测试连接管理器...")
    try:
        from src.mcp.connection_manager import MCPConnectionManager
        manager = MCPConnectionManager('config/mcp_config.yaml')
        
        print(f"   ✅ 连接管理器初始化成功")
        print(f"   - 已配置服务器: {list(manager.config.servers.keys())}")
        print(f"   - 当前连接数: {len(manager.connections)}")
        print(f"   - 禁用的服务器: {manager._disabled_servers}")
        
        return True
    except Exception as e:
        print(f"   ❌ 连接管理器初始化失败: {e}")
        return False


def test_exceptions():
    """测试异常类"""
    print("\n⚠️  测试异常类...")
    try:
        from src.mcp.exceptions import (
            MCPError, MCPConnectionError, MCPServerStartupError,
            MCPSecurityError, PermissionDeniedError
        )
        
        # 测试异常继承关系
        assert issubclass(MCPConnectionError, MCPError)
        assert issubclass(MCPServerStartupError, MCPConnectionError)
        assert issubclass(PermissionDeniedError, MCPSecurityError)
        
        # 测试异常实例化
        err1 = MCPServerStartupError("test-server", "connection timeout")
        assert "test-server" in str(err1)
        
        err2 = PermissionDeniedError("write_file", "path not allowed")
        assert "write_file" in str(err2)
        
        print("   ✅ 异常类层次结构正确")
        print("   - MCPError (基类)")
        print("   - MCPConnectionError")
        print("   - MCPSecurityError")
        print("   - MCPToolError")
        
        return True
    except Exception as e:
        print(f"   ❌ 异常测试失败: {e}")
        return False


def main():
    """运行所有验收测试"""
    print("=" * 60)
    print("Phase 3.1 验收测试")
    print("=" * 60)
    
    results = []
    results.append(test_imports())
    results.append(test_config_loading())
    results.append(test_connection_manager())
    results.append(test_exceptions())
    
    print("\n" + "=" * 60)
    print(f"测试结果: {sum(results)}/{len(results)} 通过")
    print("=" * 60)
    
    if all(results):
        print("\n🎉 所有验收测试通过！Phase 3.1 完成！")
        return 0
    else:
        print("\n❌ 部分测试失败")
        return 1


if __name__ == "__main__":
    sys.exit(main())
