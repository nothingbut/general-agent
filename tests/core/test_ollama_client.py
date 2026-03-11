"""Tests for Ollama LLM client"""
import pytest
from unittest.mock import AsyncMock, patch, MagicMock
from src.core.ollama_client import OllamaClient, OllamaConfig


@pytest.fixture
def ollama_config():
    """Create test Ollama config"""
    return OllamaConfig(
        base_url="http://localhost:11434",
        model="qwen2.5:7b",
        temperature=0.7,
        timeout=30.0
    )


@pytest.fixture
def ollama_client(ollama_config):
    """Create Ollama client with test config"""
    return OllamaClient(config=ollama_config)


@pytest.mark.asyncio
async def test_ollama_client_creation(ollama_config):
    """Test creating Ollama client"""
    client = OllamaClient(config=ollama_config)

    assert client.config.base_url == "http://localhost:11434"
    assert client.config.model == "qwen2.5:7b"
    assert client.config.temperature == 0.7


@pytest.mark.asyncio
async def test_chat_with_single_message(ollama_client):
    """Test chat with a single user message"""
    mock_response = {
        "message": {
            "role": "assistant",
            "content": "Hello! How can I help you today?"
        }
    }

    with patch('aiohttp.ClientSession.post') as mock_post:
        mock_post.return_value.__aenter__.return_value.json = AsyncMock(
            return_value=mock_response
        )
        mock_post.return_value.__aenter__.return_value.status = 200

        messages = [{"role": "user", "content": "Hello"}]
        response = await ollama_client.chat(messages)

        assert response == "Hello! How can I help you today?"
        mock_post.assert_called_once()


@pytest.mark.asyncio
async def test_chat_with_message_history(ollama_client):
    """Test chat with conversation history"""
    mock_response = {
        "message": {
            "role": "assistant",
            "content": "Your name is Alice."
        }
    }

    with patch('aiohttp.ClientSession.post') as mock_post:
        mock_post.return_value.__aenter__.return_value.json = AsyncMock(
            return_value=mock_response
        )
        mock_post.return_value.__aenter__.return_value.status = 200

        messages = [
            {"role": "user", "content": "My name is Alice"},
            {"role": "assistant", "content": "Nice to meet you Alice!"},
            {"role": "user", "content": "What's my name?"}
        ]
        response = await ollama_client.chat(messages)

        assert "Alice" in response


@pytest.mark.asyncio
async def test_chat_validates_empty_messages():
    """Test that empty messages list raises error"""
    client = OllamaClient()

    with pytest.raises(ValueError, match="Messages list cannot be empty"):
        await client.chat([])


@pytest.mark.asyncio
async def test_chat_validates_message_format():
    """Test that invalid message format raises error"""
    client = OllamaClient()

    # Missing 'role' field
    with pytest.raises(ValueError, match="Invalid message format"):
        await client.chat([{"content": "Hello"}])

    # Missing 'content' field
    with pytest.raises(ValueError, match="Invalid message format"):
        await client.chat([{"role": "user"}])


@pytest.mark.asyncio
async def test_chat_validates_empty_content():
    """Test that empty content raises error"""
    client = OllamaClient()

    with pytest.raises(ValueError, match="Message content cannot be empty"):
        await client.chat([{"role": "user", "content": ""}])


@pytest.mark.asyncio
async def test_chat_requires_user_message():
    """Test that at least one user message is required"""
    client = OllamaClient()

    messages = [
        {"role": "system", "content": "You are helpful"},
        {"role": "assistant", "content": "How can I help?"}
    ]

    with pytest.raises(ValueError, match="at least one user message"):
        await client.chat(messages)


@pytest.mark.asyncio
async def test_chat_handles_api_error(ollama_client):
    """Test handling of Ollama API errors"""
    with patch('aiohttp.ClientSession.post') as mock_post:
        mock_post.return_value.__aenter__.return_value.status = 500
        mock_post.return_value.__aenter__.return_value.text = AsyncMock(
            return_value="Internal Server Error"
        )

        messages = [{"role": "user", "content": "Hello"}]

        with pytest.raises(Exception, match="Ollama API error"):
            await ollama_client.chat(messages)


@pytest.mark.asyncio
async def test_chat_handles_connection_error(ollama_client):
    """Test handling of connection errors"""
    with patch('aiohttp.ClientSession.post', side_effect=ConnectionError("Cannot connect")):
        messages = [{"role": "user", "content": "Hello"}]

        with pytest.raises(ConnectionError):
            await ollama_client.chat(messages)


@pytest.mark.asyncio
async def test_chat_with_system_message(ollama_client):
    """Test chat with system message"""
    mock_response = {
        "message": {
            "role": "assistant",
            "content": "Bonjour!"
        }
    }

    with patch('aiohttp.ClientSession.post') as mock_post:
        mock_post.return_value.__aenter__.return_value.json = AsyncMock(
            return_value=mock_response
        )
        mock_post.return_value.__aenter__.return_value.status = 200

        messages = [
            {"role": "system", "content": "You are a French translator"},
            {"role": "user", "content": "Say hello"}
        ]
        response = await ollama_client.chat(messages)

        assert response == "Bonjour!"


@pytest.mark.asyncio
async def test_config_defaults():
    """Test default configuration values"""
    config = OllamaConfig()

    assert config.base_url == "http://localhost:11434"
    assert config.model == "qwen2.5:7b"
    assert config.temperature == 0.7
    assert config.timeout == 30.0


@pytest.mark.asyncio
async def test_config_custom_values():
    """Test custom configuration values"""
    config = OllamaConfig(
        base_url="http://custom:8080",
        model="mistral:latest",
        temperature=0.9,
        timeout=60.0
    )

    assert config.base_url == "http://custom:8080"
    assert config.model == "mistral:latest"
    assert config.temperature == 0.9
    assert config.timeout == 60.0


@pytest.mark.asyncio
async def test_request_payload_format(ollama_client):
    """Test that request payload has correct format"""
    mock_response = {
        "message": {
            "role": "assistant",
            "content": "Response"
        }
    }

    with patch('aiohttp.ClientSession.post') as mock_post:
        mock_post.return_value.__aenter__.return_value.json = AsyncMock(
            return_value=mock_response
        )
        mock_post.return_value.__aenter__.return_value.status = 200

        messages = [{"role": "user", "content": "Test"}]
        await ollama_client.chat(messages)

        # Verify the request payload
        call_kwargs = mock_post.call_args[1]
        payload = call_kwargs['json']

        assert payload['model'] == "qwen2.5:7b"
        assert payload['messages'] == messages
        assert payload['stream'] is False
        assert 'options' in payload
        assert payload['options']['temperature'] == 0.7
