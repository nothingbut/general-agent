using System.Text;
using GeneralAgent.Infrastructure.SkillExtraction.Models;
using GeneralAgent.Infrastructure.Skills.Models;
using GeneralAgent.Infrastructure.Skills.Parsers;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GeneralAgent.Infrastructure.SkillExtraction.Services;

/// <summary>
/// 技能生成器实现 - 从建议生成技能定义文件
/// </summary>
public sealed class SkillGenerator : ISkillGenerator
{
    private readonly ISkillParser _skillParser;
    private readonly ILogger<SkillGenerator> _logger;
    private readonly ISerializer _yamlSerializer;

    public SkillGenerator(
        ISkillParser skillParser,
        ILogger<SkillGenerator> logger)
    {
        _skillParser = skillParser;
        _logger = logger;

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public Task<string> GenerateSkillFileAsync(
        SkillSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("生成技能文件: {SkillName}", suggestion.FullName);

        try
        {
            // 构建 SkillMetadata
            var metadata = new SkillMetadata
            {
                Name = suggestion.Name,
                Description = suggestion.Description,
                Namespace = suggestion.Namespace,
                Parameters = suggestion.Parameters.Select(p => new SkillParameterMetadata
                {
                    Name = p.Name,
                    Type = p.Type,
                    Required = p.Required,
                    Description = p.Description,
                    DefaultValue = p.DefaultValue
                }).ToList()
            };

            // 序列化为 YAML
            var yaml = _yamlSerializer.Serialize(metadata);

            // 构建完整的技能文件
            var fileContent = new StringBuilder();
            fileContent.AppendLine("---");
            fileContent.Append(yaml);
            fileContent.AppendLine("---");
            fileContent.AppendLine();
            fileContent.AppendLine(suggestion.Template);

            var result = fileContent.ToString();

            _logger.LogDebug("生成的技能文件长度: {Length} 字符", result.Length);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成技能文件失败: {SkillName}", suggestion.FullName);
            throw;
        }
    }

    public Task<ValidationResult> ValidateSkillAsync(
        string skillContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("验证技能文件内容: {Length} 字符", skillContent.Length);

        try
        {
            // 使用现有的 SkillParser 进行验证
            var parseResult = _skillParser.Parse(skillContent);

            if (parseResult.IsSuccess)
            {
                _logger.LogDebug("技能文件验证通过");
                return Task.FromResult(ValidationResult.Success());
            }
            else
            {
                _logger.LogWarning("技能文件验证失败: {Error}", parseResult.Error);
                return Task.FromResult(ValidationResult.Failure(parseResult.Error!));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证技能文件时发生错误");
            return Task.FromResult(ValidationResult.Failure($"验证失败: {ex.Message}"));
        }
    }
}
