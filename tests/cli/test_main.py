"""Tests for CLI main entry point"""
import pytest
from typer.testing import CliRunner
from src.cli.__main__ import cli

runner = CliRunner()


def test_no_args_shows_help():
    """Test displaying help without arguments"""
    result = runner.invoke(cli, [])
    assert result.exit_code == 0
    assert "用法" in result.stdout or "Usage" in result.stdout or "agent" in result.stdout


def test_help_flag():
    """Test --help flag"""
    result = runner.invoke(cli, ["--help"])
    assert result.exit_code == 0
    assert "agent" in result.stdout.lower()


def test_version_flag():
    """Test --version flag"""
    result = runner.invoke(cli, ["--version"])
    assert result.exit_code == 0
    assert "General Agent" in result.stdout or "version" in result.stdout.lower()
