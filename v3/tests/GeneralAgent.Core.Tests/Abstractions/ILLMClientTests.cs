using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Models;

namespace GeneralAgent.Core.Tests.Abstractions;

/// <summary>
/// 测试 ILLMClient 接口契约（编译时验证）
/// </summary>
public class ILLMClientTests
{
    [Fact]
    public void ILLMClient_HasProviderNameProperty()
    {
        // 接口必须有 ProviderName 属性
        var property = typeof(ILLMClient).GetProperty(nameof(ILLMClient.ProviderName));
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
    }

    [Fact]
    public void ILLMClient_HasCompleteAsyncMethod()
    {
        // 接口必须有 CompleteAsync 方法
        var method = typeof(ILLMClient).GetMethod(nameof(ILLMClient.CompleteAsync));
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<CompletionResponse>), method.ReturnType);
    }

    [Fact]
    public void ILLMClient_HasStreamAsyncMethod()
    {
        // 接口必须有 StreamAsync 方法
        var method = typeof(ILLMClient).GetMethod(nameof(ILLMClient.StreamAsync));
        Assert.NotNull(method);
        // 验证返回类型是 IAsyncEnumerable<StreamChunk>
        Assert.True(method.ReturnType.IsGenericType);
        Assert.Equal(typeof(IAsyncEnumerable<>), method.ReturnType.GetGenericTypeDefinition());
    }
}

/// <summary>
/// 测试 ILLMClientFactory 接口契约
/// </summary>
public class ILLMClientFactoryTests
{
    [Fact]
    public void ILLMClientFactory_HasGetClientMethod()
    {
        var method = typeof(ILLMClientFactory).GetMethod(nameof(ILLMClientFactory.GetClient));
        Assert.NotNull(method);
        Assert.Equal(typeof(ILLMClient), method.ReturnType);
    }

    [Fact]
    public void ILLMClientFactory_HasGetAvailableProvidersMethod()
    {
        var method = typeof(ILLMClientFactory).GetMethod(nameof(ILLMClientFactory.GetAvailableProviders));
        Assert.NotNull(method);
        Assert.Equal(typeof(IReadOnlyList<string>), method.ReturnType);
    }
}
