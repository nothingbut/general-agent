using GeneralAgent.Application.Models;
using GeneralAgent.Core.Common;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 配置服务接口
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// 获取当前配置（包含环境变量覆盖）
    /// </summary>
    Task<Result<UserConfig>> GetConfigAsync();

    /// <summary>
    /// 保存配置
    /// </summary>
    Task<Result<bool>> SaveConfigAsync(UserConfig config);

    /// <summary>
    /// 更新配置项
    /// </summary>
    Task<Result<bool>> UpdateConfigAsync(string key, string value);

    /// <summary>
    /// 重置配置到默认值
    /// </summary>
    Task<Result<bool>> ResetConfigAsync();

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    string GetConfigFilePath();
}
