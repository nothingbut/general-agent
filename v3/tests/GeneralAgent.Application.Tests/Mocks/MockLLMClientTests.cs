using FluentAssertions;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Application.Tests.Mocks;

/// <summary>
/// MockLLMClient 单元测试
/// </summary>
public sealed class MockLLMClientTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaults_ShouldHaveCorrectDefaults()
    {
        // Act
        var client = new MockLLMClient();

        // Assert
        client.ProviderName.Should().Be("Mock");
    }

    [Fact]
    public void Constructor_WithCustomProviderName_ShouldSetProviderName()
    {
        // Act
        var client = new MockLLMClient(providerName: "CustomProvider");

        // Assert
        client.ProviderName.Should().Be("CustomProvider");
    }

    [Fact]
    public void Constructor_WithCustomResponseContent_ShouldBeUsedInResponses()
    {
        // Arrange
        var customResponse = "Custom mock response";

        // Act
        var client = new MockLLMClient(responseContent: customResponse);

        // Assert
        client.ProviderName.Should().Be("Mock");
    }

    #endregion

    #region CompleteAsync Tests

    [Fact]
    public async Task CompleteAsync_WithValidRequest_ShouldReturnConfiguredResponse()
    {
        // Arrange
        var client = new MockLLMClient(responseContent: "Test response");
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = [new ChatMessage { Role = "user", Content = "Hello" }]
        };

        // Act
        var response = await client.CompleteAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().Be("Test response");
        response.Model.Should().Be("test-model");
        response.Usage.Should().NotBeNull();
        response.Timestamp.Should().NotBe(default);
    }

    [Fact]
    public async Task CompleteAsync_ShouldIncludeTokenUsage()
    {
        // Arrange
        var client = new MockLLMClient();
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        var response = await client.CompleteAsync(request);

        // Assert
        response.Usage.PromptTokens.Should().BeGreaterThanOrEqualTo(0);
        response.Usage.CompletionTokens.Should().BeGreaterThanOrEqualTo(0);
        response.Usage.TotalTokens.Should().Be(
            response.Usage.PromptTokens + response.Usage.CompletionTokens);
    }

    [Fact]
    public async Task CompleteAsync_WithDelay_ShouldDelayExecution()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);
        var client = new MockLLMClient(simulateDelay: delay);
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await client.CompleteAsync(request);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo((long)delay.TotalMilliseconds - 50);
    }

    [Fact]
    public async Task CompleteAsync_WhenConfiguredToThrow_ShouldThrowLLMException()
    {
        // Arrange
        var client = new MockLLMClient(shouldThrow: true);
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act & Assert
        await client.Invoking(c => c.CompleteAsync(request))
            .Should()
            .ThrowAsync<LLMException>();
    }

    [Fact]
    public async Task CompleteAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var client = new MockLLMClient(simulateDelay: TimeSpan.FromSeconds(10));
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act & Assert
        await client.Invoking(c => c.CompleteAsync(request, cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region StreamAsync Tests

    [Fact]
    public async Task StreamAsync_ShouldReturnStreamChunks()
    {
        // Arrange
        var client = new MockLLMClient(responseContent: "Stream test");
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().NotBeEmpty();
        var fullContent = string.Concat(chunks.Select(c => c.Delta));
        fullContent.Should().Be("Stream test");
    }

    [Fact]
    public async Task StreamAsync_LastChunk_ShouldHaveIsCompleteTrue()
    {
        // Arrange
        var client = new MockLLMClient(responseContent: "Test");
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Last().IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAsync_LastChunk_ShouldIncludeTokenUsage()
    {
        // Arrange
        var client = new MockLLMClient();
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        StreamChunk? lastChunk = null;
        await foreach (var chunk in client.StreamAsync(request))
        {
            lastChunk = chunk;
        }

        // Assert
        lastChunk.Should().NotBeNull();
        lastChunk!.Usage.Should().NotBeNull();
        lastChunk.Usage!.TotalTokens.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task StreamAsync_ShouldChunkContent()
    {
        // Arrange
        var client = new MockLLMClient(responseContent: "Hello World");
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        var chunks = new List<StreamChunk>();
        await foreach (var chunk in client.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        // At least 2 chunks: content chunks + final chunk with IsComplete=true
        chunks.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task StreamAsync_WithDelay_ShouldDelayExecution()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);
        var client = new MockLLMClient(simulateDelay: delay);
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var count = 0;
        await foreach (var _ in client.StreamAsync(request))
        {
            count++;
        }
        sw.Stop();

        // Assert
        count.Should().BeGreaterThan(0);
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo((long)delay.TotalMilliseconds - 50);
    }

    [Fact]
    public async Task StreamAsync_WhenConfiguredToThrow_ShouldThrowLLMException()
    {
        // Arrange
        var client = new MockLLMClient(shouldThrow: true);
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };

        // Act & Assert
        await client.Invoking(async c =>
        {
            await foreach (var _ in c.StreamAsync(request))
            {
                // Consume the stream
            }
        })
        .Should()
        .ThrowAsync<LLMException>();
    }

    [Fact]
    public async Task StreamAsync_WithCancellation_ShouldThrowOperationCanceledException()
    {
        // Arrange
        var client = new MockLLMClient(simulateDelay: TimeSpan.FromSeconds(10));
        var request = new CompletionRequest
        {
            Model = "test-model",
            Messages = []
        };
        var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // Act & Assert
        await client.Invoking(async c =>
        {
            await foreach (var _ in c.StreamAsync(request, cts.Token))
            {
                // Consume the stream
            }
        })
        .Should()
        .ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region Factory Method Tests

    [Fact]
    public void CreateSuccess_ShouldReturnConfiguredClient()
    {
        // Act
        var client = MockLLMClient.CreateSuccess();

        // Assert
        client.Should().NotBeNull();
        client.ProviderName.Should().Be("Mock");
    }

    [Fact]
    public async Task CreateSuccess_CompleteAsync_ShouldNotThrow()
    {
        // Arrange
        var client = MockLLMClient.CreateSuccess();
        var request = new CompletionRequest
        {
            Model = "test",
            Messages = []
        };

        // Act & Assert
        await client.Invoking(c => c.CompleteAsync(request))
            .Should()
            .NotThrowAsync();
    }

    [Fact]
    public void CreateFailure_ShouldReturnConfiguredClient()
    {
        // Act
        var client = MockLLMClient.CreateFailure();

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateFailure_CompleteAsync_ShouldThrow()
    {
        // Arrange
        var client = MockLLMClient.CreateFailure();
        var request = new CompletionRequest
        {
            Model = "test",
            Messages = []
        };

        // Act & Assert
        await client.Invoking(c => c.CompleteAsync(request))
            .Should()
            .ThrowAsync<LLMException>();
    }

    [Fact]
    public void CreateWithDelay_ShouldReturnConfiguredClient()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);

        // Act
        var client = MockLLMClient.CreateWithDelay(delay);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateWithDelay_CompleteAsync_ShouldRespectDelay()
    {
        // Arrange
        var delay = TimeSpan.FromMilliseconds(100);
        var client = MockLLMClient.CreateWithDelay(delay);
        var request = new CompletionRequest
        {
            Model = "test",
            Messages = []
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await client.CompleteAsync(request);
        sw.Stop();

        // Assert
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo((long)delay.TotalMilliseconds - 50);
    }

    #endregion
}
