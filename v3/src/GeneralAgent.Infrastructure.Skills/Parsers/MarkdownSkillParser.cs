using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeneralAgent.Core.Common;
using GeneralAgent.Infrastructure.Skills.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GeneralAgent.Infrastructure.Skills.Parsers;

/// <summary>
/// Markdown 技能解析器
/// 解析 YAML frontmatter + Markdown 内容
/// </summary>
public partial class MarkdownSkillParser : ISkillParser
{
    private static readonly Regex FrontmatterRegex = GetFrontmatterRegex();

    private readonly IDeserializer _yamlDeserializer;

    public MarkdownSkillParser()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public Result<Skill> Parse(string content)
    {
        try
        {
            // 提取 YAML frontmatter 和内容
            var match = FrontmatterRegex.Match(content);
            if (!match.Success)
            {
                return Result<Skill>.Failure(
                    "技能文件必须包含 YAML frontmatter（以 --- 包围）");
            }

            var yamlContent = match.Groups[1].Value;
            var templateContent = match.Groups[2].Value.Trim();

            // 解析 YAML
            var metadata = _yamlDeserializer.Deserialize<SkillMetadata>(yamlContent);

            // 验证必填字段
            if (string.IsNullOrWhiteSpace(metadata.Name))
            {
                return Result<Skill>.Failure("技能名称（name）不能为空");
            }

            if (string.IsNullOrWhiteSpace(metadata.Description))
            {
                return Result<Skill>.Failure("技能描述（description）不能为空");
            }

            // 转换参数
            var parameters = metadata.Parameters
                .Select(p => new SkillParameter
                {
                    Name = p.Name,
                    Type = p.Type,
                    Required = p.Required,
                    Description = p.Description,
                    DefaultValue = p.DefaultValue
                })
                .ToList();

            // 构建技能对象
            var skill = new Skill
            {
                Name = metadata.Name,
                Description = metadata.Description,
                Template = templateContent,
                Parameters = parameters,
                Namespace = metadata.Namespace,
                Tags = metadata.Tags
            };

            return Result<Skill>.Success(skill);
        }
        catch (Exception ex)
        {
            return Result<Skill>.Failure($"解析技能文件失败: {ex.Message}");
        }
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n(.*)$", RegexOptions.Singleline)]
    private static partial Regex GetFrontmatterRegex();
}
