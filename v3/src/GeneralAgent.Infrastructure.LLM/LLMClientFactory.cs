using GeneralAgent.Core.Abstractions;

namespace GeneralAgent.Infrastructure.LLM;

/// <summary>
/// LLM 客户端工厂实现
/// (Task 9: LLM - 实现客户端工厂)
/// </summary>
internal sealed class LLMClientFactory : ILLMClientFactory
{
    /// <summary>
    /// 获取指定提供商的客户端
    /// </summary>
    /// <param name="providerName">提供商名称</param>
    /// <returns>LLM 客户端实例</returns>
    /// <exception cref="Core.Exceptions.LLMException">提供商未配置</exception>
    public ILLMClient GetClient(string providerName)
    {
        throw new NotImplementedException("将在 Task 9 中实现");
    }

    /// <summary>
    /// 获取所有已配置的提供商名称
    /// </summary>
    /// <returns>提供商名称列表</returns>
    public IReadOnlyList<string> GetAvailableProviders()
    {
        throw new NotImplementedException("将在 Task 9 中实现");
    }
}
