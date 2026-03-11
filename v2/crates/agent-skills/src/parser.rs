//! 技能文件解析器
//!
//! 负责解析 Markdown + YAML frontmatter 格式的技能文件

use crate::models::SkillDefinition;
use std::path::Path;
use thiserror::Error;

/// 解析器错误类型
#[derive(Debug, Error)]
pub enum ParseError {
    #[error("Invalid frontmatter: {0}")]
    InvalidFrontmatter(String),

    #[error("Missing frontmatter")]
    MissingFrontmatter,

    #[error("YAML parsing error: {0}")]
    YamlError(#[from] serde_yaml::Error),

    #[error("Missing required field: {0}")]
    MissingField(String),
}

pub type Result<T> = std::result::Result<T, ParseError>;

/// 技能文件解析器
pub struct SkillParser;

impl SkillParser {
    /// 解析技能文件
    pub fn parse(content: &str, file_path: &Path) -> Result<SkillDefinition> {
        // 1. 分离 YAML frontmatter 和 Markdown
        let (yaml_str, markdown_str) = Self::split_frontmatter(content)?;

        // 2. 解析 YAML
        let mut skill: SkillDefinition = serde_yaml::from_str(&yaml_str)?;

        // 3. 设置内容和路径
        skill.content = markdown_str.to_string();
        skill.file_path = file_path.to_path_buf();

        // 4. 验证
        Self::validate(&skill)?;

        Ok(skill)
    }

    /// 分离 frontmatter 和 Markdown 内容
    fn split_frontmatter(content: &str) -> Result<(String, String)> {
        // 查找 YAML frontmatter 的起止分隔符 "---"
        let lines: Vec<&str> = content.lines().collect();

        if lines.is_empty() || lines[0].trim() != "---" {
            return Err(ParseError::MissingFrontmatter);
        }

        // 查找第二个 "---"
        let mut end_index = None;
        for (i, line) in lines.iter().enumerate().skip(1) {
            if line.trim() == "---" {
                end_index = Some(i);
                break;
            }
        }

        let end_index = end_index.ok_or(ParseError::MissingFrontmatter)?;

        // 提取 YAML 部分（不包括分隔符）
        let yaml_lines = &lines[1..end_index];
        let yaml_str = yaml_lines.join("\n");

        // 提取 Markdown 部分（第二个 --- 之后的所有内容）
        let markdown_lines = &lines[end_index + 1..];
        let markdown_str = markdown_lines.join("\n");

        Ok((yaml_str, markdown_str.trim().to_string()))
    }

    /// 验证技能定义
    fn validate(skill: &SkillDefinition) -> Result<()> {
        if skill.name.trim().is_empty() {
            return Err(ParseError::MissingField("name".to_string()));
        }

        if skill.description.trim().is_empty() {
            return Err(ParseError::MissingField("description".to_string()));
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    #[test]
    fn test_parse_basic_skill_without_parameters() {
        let content = r#"---
name: greeting
description: Greet the user warmly
---

# Greeting Skill

Hello! I'm here to assist you.
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        assert_eq!(skill.name, "greeting");
        assert_eq!(skill.description, "Greet the user warmly");
        assert_eq!(
            skill.content.trim(),
            "# Greeting Skill\n\nHello! I'm here to assist you."
        );
        assert!(skill.parameters.is_empty());
    }

    #[test]
    fn test_parse_skill_with_parameters() {
        let content = r#"---
name: greeting
description: Greet the user with custom name and tone
parameters:
  - name: user_name
    type: string
    required: true
    description: User's name
  - name: tone
    type: string
    required: false
    description: Greeting tone
    default: friendly
---

# Greeting Skill

Hello {user_name}! I'm here to assist you in a {tone} manner.
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        assert_eq!(skill.name, "greeting");
        assert_eq!(
            skill.description,
            "Greet the user with custom name and tone"
        );
        assert_eq!(skill.parameters.len(), 2);

        // 验证第一个参数
        let param1 = &skill.parameters[0];
        assert_eq!(param1.name, "user_name");
        assert_eq!(param1.param_type, "string");
        assert!(param1.required);
        assert_eq!(param1.description, "User's name");
        assert!(param1.default.is_none());

        // 验证第二个参数
        let param2 = &skill.parameters[1];
        assert_eq!(param2.name, "tone");
        assert_eq!(param2.param_type, "string");
        assert!(!param2.required);
        assert_eq!(param2.description, "Greeting tone");
        assert_eq!(param2.default, Some("friendly".to_string()));
    }

    #[test]
    fn test_parse_missing_frontmatter() {
        let content = r#"# Greeting Skill

Hello! I'm here to assist you.
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            ParseError::MissingFrontmatter
        ));
    }

    #[test]
    fn test_parse_incomplete_frontmatter() {
        let content = r#"---
name: greeting
description: Greet the user

# Missing closing ---

Hello! I'm here to assist you.
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            ParseError::MissingFrontmatter
        ));
    }

    #[test]
    fn test_parse_invalid_yaml() {
        let content = r#"---
name: greeting
description: [invalid yaml: missing closing bracket
---

Hello!
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        assert!(matches!(result.unwrap_err(), ParseError::YamlError(_)));
    }

    #[test]
    fn test_parse_missing_name() {
        let content = r#"---
description: Greet the user
---

Hello!
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        // YAML 解析会失败，因为 name 是必需字段
    }

    #[test]
    fn test_parse_missing_description() {
        let content = r#"---
name: greeting
---

Hello!
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        // YAML 解析会失败，因为 description 是必需字段
    }

    #[test]
    fn test_parse_empty_name() {
        let content = r#"---
name: ""
description: Greet the user
---

Hello!
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        assert!(matches!(result.unwrap_err(), ParseError::MissingField(field) if field == "name"));
    }

    #[test]
    fn test_parse_empty_description() {
        let content = r#"---
name: greeting
description: ""
---

Hello!
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_err());
        assert!(
            matches!(result.unwrap_err(), ParseError::MissingField(field) if field == "description")
        );
    }

    #[test]
    fn test_parse_empty_markdown_content() {
        let content = r#"---
name: greeting
description: Greet the user
---

"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        assert!(skill.content.is_empty());
    }

    #[test]
    fn test_parse_whitespace_only_markdown() {
        let content = r#"---
name: greeting
description: Greet the user
---



"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        // trim() should remove whitespace
        assert!(skill.content.trim().is_empty());
    }

    #[test]
    fn test_parse_file_path_preserved() {
        let content = r#"---
name: greeting
description: Greet the user
---

Hello!
"#;

        let path = PathBuf::from("/path/to/skills/greeting.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        assert_eq!(
            skill.file_path,
            PathBuf::from("/path/to/skills/greeting.md")
        );
    }

    #[test]
    fn test_parse_multiline_description() {
        let content = r#"---
name: greeting
description: |
  This is a multiline description
  that spans multiple lines
  and should be preserved
---

Hello!
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        assert!(skill.description.contains("multiline description"));
        assert!(skill.description.contains("multiple lines"));
    }

    #[test]
    fn test_parse_complex_markdown_with_code_blocks() {
        let content = r#"---
name: coding
description: Coding assistance
---

# Coding Skill

```rust
fn main() {
    println!("Hello");
}
```

More content here.
"#;

        let path = PathBuf::from("test.md");
        let result = SkillParser::parse(content, &path);

        assert!(result.is_ok());
        let skill = result.unwrap();
        assert!(skill.content.contains("```rust"));
        assert!(skill.content.contains("fn main()"));
    }
}
