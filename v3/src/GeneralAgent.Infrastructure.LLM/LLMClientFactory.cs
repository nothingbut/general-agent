using System.Collections.Concurrent;
using GeneralAgent.Core.Abstractions;
using GeneralAgent.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// LLM 客户端工厂实现
/// </summary>
internal sealed class LLMClientFactory : ILLMClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LLMOptions> _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, ILLMClient> _clients;

    public LLMClientFactory(
        IHttpClientFactory httpClientFactory,
        IOptions<LLMOptions> options,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _clients = new ConcurrentDictionary<string, ILLMClient>();
    }

    /// <inheritdoc/>
    public ILLMClient GetClient(string? providerName = null)
    {
        var name = providerName ?? _options.Value.DefaultProvider;

        if (string.IsNullOrWhiteSpace(name))
            throw new LLMException("未配置默认 LLM 提供商");

        return _clients.GetOrAdd(name, CreateClient);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAvailableProviders()
    {
        return _options.Value.Providers.Keys.ToList();
    }

    private ILLMClient CreateClient(string providerName)
    {
        if (!_options.Value.Providers.TryGetValue(providerName, out var config))
            throw new LLMException($"提供商 '{providerName}' 未配置");

        var httpClient = _httpClientFactory.CreateClient($"LLM_{providerName}");
        var providerOptions = Options.Create(config);
        var logger = _loggerFactory.CreateLogger<OpenAICompatibleClient>();

        return new OpenAICompatibleClient(httpClient, providerOptions, logger);
    }
}
