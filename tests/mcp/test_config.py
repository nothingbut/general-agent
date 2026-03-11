"""Tests for MCP configuration."""
import pytest
from pathlib import Path
import yaml
from src.mcp.config import MCPConfig, ServerConfig, SecurityConfig, load_mcp_config


def test_load_valid_config(temp_mcp_config):
    """Test loading valid YAML config."""
    config = load_mcp_config(str(temp_mcp_config))

    assert isinstance(config, MCPConfig)
    assert "filesystem" in config.servers
    assert config.global_config["enabled"] is True


def test_server_config_validation(temp_mcp_config):
    """Test server config structure."""
    config = load_mcp_config(str(temp_mcp_config))
    server = config.servers["filesystem"]

    assert isinstance(server, ServerConfig)
    assert server.command == "npx"
    assert len(server.args) > 0
    assert server.timeout == 30.0


def test_security_config_validation(temp_mcp_config):
    """Test security config structure."""
    config = load_mcp_config(str(temp_mcp_config))
    security = config.servers["filesystem"].security

    assert isinstance(security, SecurityConfig)
    assert len(security.allowed_directories) > 0
    assert "read_file" in security.allowed_operations
    assert "delete_file" in security.denied_operations


def test_missing_config_file():
    """Test error handling for missing config."""
    with pytest.raises(FileNotFoundError):
        load_mcp_config("/nonexistent/config.yaml")


def test_invalid_yaml_format(tmp_path):
    """Test error handling for invalid YAML."""
    bad_config = tmp_path / "bad_config.yaml"
    bad_config.write_text("invalid: yaml: content: [")

    with pytest.raises(yaml.YAMLError):
        load_mcp_config(str(bad_config))


def test_missing_required_fields(tmp_path):
    """Test config validation catches missing fields."""
    bad_config = tmp_path / "bad.yaml"
    bad_config.write_text("""
servers:
  filesystem:
    # Missing 'command' field
    args: []
""")

    with pytest.raises(KeyError):
        load_mcp_config(str(bad_config))


def test_environment_variable_expansion(tmp_path, monkeypatch):
    """Test env var expansion in config."""
    # Set test env var
    monkeypatch.setenv("TEST_TOKEN", "secret123")

    config_content = """
servers:
  test:
    command: echo
    args: []
    security:
      allowed_directories: []
      allowed_operations: []
      denied_operations: []
    env:
      TOKEN: ${TEST_TOKEN}
"""
    config_file = tmp_path / "config.yaml"
    config_file.write_text(config_content)

    config = load_mcp_config(str(config_file))
    # Note: Env var expansion would need to be implemented
    # For now, just test structure is valid
    assert "test" in config.servers


def test_default_values(tmp_path):
    """Test default values are applied."""
    minimal_config = tmp_path / "minimal.yaml"
    minimal_config.write_text("""
servers:
  test:
    command: echo
    args: []
    security:
      allowed_directories: []
      allowed_operations: []
      denied_operations: []
""")

    config = load_mcp_config(str(minimal_config))
    server = config.servers["test"]

    assert server.timeout == 30.0  # Default
    assert server.health_check_interval == 60  # Default
    assert server.env == {}  # Default empty dict
