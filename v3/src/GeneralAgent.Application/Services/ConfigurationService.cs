using System.Text.Json;
using GeneralAgent.Application.Models;
using GeneralAgent.Core.Common;
using Microsoft.Extensions.Logging;

namespace GeneralAgent.Application.Services;

/// <summary>
/// 配置服务实现
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 配置文件路径：~/.agent/config.json
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, ".agent");

        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
            _logger.LogInformation("创建配置目录: {ConfigDir}", configDir);
        }

        _configFilePath = Path.Combine(configDir, "config.json");
    }

    public async Task<Result<UserConfig>> GetConfigAsync()
    {
        try
        {
            UserConfig config;

            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                config = JsonSerializer.Deserialize<UserConfig>(json, JsonOptions)
                    ?? UserConfig.Default();
                _logger.LogDebug("从文件加载配置: {Path}", _configFilePath);
            }
            else
            {
                config = UserConfig.Default();
                _logger.LogInformation("使用默认配置");
            }

            // 应用环境变量覆盖
            config = config.ApplyEnvironmentVariables();

            return Result<UserConfig>.Success(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取配置失败");
            return Result<UserConfig>.Failure($"读取配置失败: {ex.Message}");
        }
    }

    public async Task<Result<bool>> SaveConfigAsync(UserConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json);

            _logger.LogInformation("配置已保存: {Path}", _configFilePath);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败");
            return Result<bool>.Failure($"保存配置失败: {ex.Message}");
        }
    }

    public async Task<Result<bool>> UpdateConfigAsync(string key, string value)
    {
        try
        {
            var configResult = await GetConfigAsync();
            if (!configResult.IsSuccess)
            {
                return Result<bool>.Failure(configResult.Error!);
            }

            var config = configResult.Value!;

            // 使用反射更新属性
            var property = typeof(UserConfig).GetProperty(key);
            if (property == null)
            {
                return Result<bool>.Failure($"未知的配置项: {key}");
            }

            if (!property.CanWrite)
            {
                return Result<bool>.Failure($"配置项 {key} 不可修改");
            }

            // 转换值类型
            object? convertedValue;
            try
            {
                if (property.PropertyType == typeof(string))
                {
                    convertedValue = value;
                }
                else if (property.PropertyType == typeof(int))
                {
                    convertedValue = int.Parse(value);
                }
                else if (property.PropertyType == typeof(bool))
                {
                    convertedValue = bool.Parse(value);
                }
                else
                {
                    return Result<bool>.Failure($"不支持的配置类型: {property.PropertyType}");
                }
            }
            catch (FormatException)
            {
                return Result<bool>.Failure($"值格式错误: {value}，期望类型: {property.PropertyType.Name}");
            }

            // 使用 with 表达式更新配置
            var updatedConfig = config with { };
            property.SetValue(updatedConfig, convertedValue);

            return await SaveConfigAsync(updatedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新配置失败");
            return Result<bool>.Failure($"更新配置失败: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ResetConfigAsync()
    {
        try
        {
            var defaultConfig = UserConfig.Default();
            await File.WriteAllTextAsync(_configFilePath,
                JsonSerializer.Serialize(defaultConfig, JsonOptions));

            _logger.LogInformation("配置已重置为默认值");
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置配置失败");
            return Result<bool>.Failure($"重置配置失败: {ex.Message}");
        }
    }

    public string GetConfigFilePath() => _configFilePath;
}
