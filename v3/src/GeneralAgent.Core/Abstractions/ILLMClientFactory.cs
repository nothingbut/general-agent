namespace GeneralAgent.Core.Abstractions;

/// <summary>
/// LLM 客户端工厂，支持多提供商管理
/// </summary>
public interface ILLMClientFactory
{
    /// <summary>
    /// 获取指定提供商的客户端。如果未指定提供商，使用默认提供商。
    /// </summary>
    /// <param name="providerName">提供商名称（可选，默认使用配置的默认提供商）</param>
    /// <returns>LLM 客户端实例</returns>
    /// <exception cref="Exceptions.LLMException">提供商未配置或默认提供商未配置</exception>
    ILLMClient GetClient(string? providerName = null);

    /// <summary>
    /// 获取所有已配置的提供商名称
    /// </summary>
    /// <returns>提供商名称列表</returns>
    IReadOnlyList<string> GetAvailableProviders();
}
