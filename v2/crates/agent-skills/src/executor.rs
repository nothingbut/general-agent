//! 技能执行器
//!
//! 负责解析技能调用语法并执行技能

use crate::models::{SkillDefinition, SkillExecutionContext};
use regex::Regex;
use std::collections::HashMap;
use thiserror::Error;

/// 执行器错误类型
#[derive(Debug, Error)]
pub enum ExecutorError {
    #[error("Invalid invocation syntax: {0}")]
    InvalidSyntax(String),

    #[error("Validation error: {0}")]
    ValidationError(String),
}

pub type Result<T> = std::result::Result<T, ExecutorError>;

/// 技能执行器
#[derive(Debug)]
pub struct SkillExecutor {
    skill_pattern: Regex,
    param_pattern: Regex,
}

impl SkillExecutor {
    /// 创建新的执行器
    pub fn new() -> Self {
        Self {
            skill_pattern: Regex::new(r"^[@/](\S+)").unwrap(),
            param_pattern: Regex::new(r#"(\w+)=['"]([^'"]+)['"]"#).unwrap(),
        }
    }

    /// 解析技能调用
    /// 支持两种格式:
    /// 1. 完整格式: "@greeting user_name='Alice' tone='friendly'"
    /// 2. 简写格式: "@greeting Alice" (仅当第一个参数是必需参数时)
    /// 输出: (skill_name, parameters)
    pub fn parse_invocation(&self, input: &str) -> Result<(String, HashMap<String, String>)> {
        let input = input.trim();

        // 1. 提取技能名称
        let skill_name = self
            .skill_pattern
            .captures(input)
            .and_then(|caps| caps.get(1))
            .map(|m| m.as_str().to_string())
            .ok_or_else(|| {
                ExecutorError::InvalidSyntax(format!("Invalid skill invocation format: {}", input))
            })?;

        // 2. 提取参数
        let mut parameters = HashMap::new();
        for cap in self.param_pattern.captures_iter(input) {
            if let (Some(key), Some(value)) = (cap.get(1), cap.get(2)) {
                parameters.insert(key.as_str().to_string(), value.as_str().to_string());
            }
        }

        // 3. 如果没有找到任何参数，尝试简写格式（提取技能名后的所有内容作为第一个参数值）
        if parameters.is_empty() {
            if let Some(caps) = self.skill_pattern.captures(input) {
                if let Some(matched) = caps.get(0) {
                    let remaining = input[matched.end()..].trim();
                    if !remaining.is_empty() {
                        // 将剩余内容作为未命名参数保存，后续由 execute 方法处理
                        parameters.insert("__positional_0".to_string(), remaining.to_string());
                    }
                }
            }
        }

        Ok((skill_name, parameters))
    }

    /// 执行技能
    pub fn execute(
        &self,
        skill: &SkillDefinition,
        mut parameters: HashMap<String, String>,
    ) -> Result<String> {
        // 1. 处理位置参数（简写语法）
        if let Some(value) = parameters.remove("__positional_0") {
            // 找到第一个必需参数
            if let Some(first_required_param) = skill.parameters.iter().find(|p| p.required) {
                parameters.insert(first_required_param.name.clone(), value);
            } else {
                return Err(ExecutorError::InvalidSyntax(
                    "Cannot use positional argument: skill has no required parameters".to_string()
                ));
            }
        }

        // 2. 创建执行上下文
        let context = SkillExecutionContext::new(skill.clone(), parameters);

        // 3. 验证参数
        context.validate().map_err(ExecutorError::ValidationError)?;

        // 4. 构建提示词
        let prompt = context.build_prompt();

        Ok(prompt)
    }
}

impl Default for SkillExecutor {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::SkillParameter;

    // 测试辅助函数：创建测试技能
    fn create_test_skill() -> SkillDefinition {
        let mut skill = SkillDefinition::new("greeting".to_string(), "Greet the user".to_string());
        skill.content = "Hello {user_name}! Tone: {tone}".to_string();

        skill.parameters.push(SkillParameter::new(
            "user_name".to_string(),
            "string".to_string(),
            true,
            "User's name".to_string(),
        ));

        skill.parameters.push(
            SkillParameter::new(
                "tone".to_string(),
                "string".to_string(),
                false,
                "Greeting tone".to_string(),
            )
            .with_default("friendly".to_string()),
        );

        skill
    }

    #[test]
    fn test_parse_invocation_with_at_symbol() {
        let executor = SkillExecutor::new();
        let input = "@greeting user_name='Alice'";

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (skill_name, params) = result.unwrap();
        assert_eq!(skill_name, "greeting");
        assert_eq!(params.get("user_name"), Some(&"Alice".to_string()));
    }

    #[test]
    fn test_parse_invocation_with_slash() {
        let executor = SkillExecutor::new();
        let input = "/greeting user_name='Bob'";

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (skill_name, params) = result.unwrap();
        assert_eq!(skill_name, "greeting");
        assert_eq!(params.get("user_name"), Some(&"Bob".to_string()));
    }

    #[test]
    fn test_parse_invocation_multiple_parameters() {
        let executor = SkillExecutor::new();
        let input = "@greeting user_name='Alice' tone='friendly' lang='en'";

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (skill_name, params) = result.unwrap();
        assert_eq!(skill_name, "greeting");
        assert_eq!(params.get("user_name"), Some(&"Alice".to_string()));
        assert_eq!(params.get("tone"), Some(&"friendly".to_string()));
        assert_eq!(params.get("lang"), Some(&"en".to_string()));
    }

    #[test]
    fn test_parse_invocation_with_namespace() {
        let executor = SkillExecutor::new();
        let input = "@personal:greeting user_name='Alice'";

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (skill_name, _) = result.unwrap();
        assert_eq!(skill_name, "personal:greeting");
    }

    #[test]
    fn test_parse_invocation_no_parameters() {
        let executor = SkillExecutor::new();
        let input = "@help";

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (skill_name, params) = result.unwrap();
        assert_eq!(skill_name, "help");
        assert!(params.is_empty());
    }

    #[test]
    fn test_parse_invocation_double_quotes() {
        let executor = SkillExecutor::new();
        let input = r#"@greeting user_name="Alice""#;

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (_, params) = result.unwrap();
        assert_eq!(params.get("user_name"), Some(&"Alice".to_string()));
    }

    #[test]
    fn test_parse_invocation_with_spaces() {
        let executor = SkillExecutor::new();
        let input = r#"@greeting user_name="Alice Smith""#;

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (_, params) = result.unwrap();
        assert_eq!(params.get("user_name"), Some(&"Alice Smith".to_string()));
    }

    #[test]
    fn test_parse_invocation_invalid_format() {
        let executor = SkillExecutor::new();
        let input = "greeting user_name='Alice'"; // 缺少 @ 或 /

        let result = executor.parse_invocation(input);
        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            ExecutorError::InvalidSyntax(_)
        ));
    }

    #[test]
    fn test_execute_skill_success() {
        let executor = SkillExecutor::new();
        let skill = create_test_skill();

        let mut params = HashMap::new();
        params.insert("user_name".to_string(), "Alice".to_string());
        params.insert("tone".to_string(), "professional".to_string());

        let result = executor.execute(&skill, params);
        assert!(result.is_ok());

        let prompt = result.unwrap();
        assert_eq!(prompt, "Hello Alice! Tone: professional");
    }

    #[test]
    fn test_execute_skill_with_default() {
        let executor = SkillExecutor::new();
        let skill = create_test_skill();

        let mut params = HashMap::new();
        params.insert("user_name".to_string(), "Bob".to_string());
        // tone 使用默认值

        let result = executor.execute(&skill, params);
        assert!(result.is_ok());

        let prompt = result.unwrap();
        assert_eq!(prompt, "Hello Bob! Tone: friendly");
    }

    #[test]
    fn test_execute_skill_missing_required_parameter() {
        let executor = SkillExecutor::new();
        let skill = create_test_skill();

        let params = HashMap::new(); // 缺少必需的 user_name

        let result = executor.execute(&skill, params);
        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            ExecutorError::ValidationError(_)
        ));
    }

    #[test]
    fn test_execute_skill_empty_parameter() {
        let executor = SkillExecutor::new();
        let skill = create_test_skill();

        let mut params = HashMap::new();
        params.insert("user_name".to_string(), "   ".to_string()); // 空白值

        let result = executor.execute(&skill, params);
        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            ExecutorError::ValidationError(_)
        ));
    }

    #[test]
    fn test_parse_invocation_shorthand_syntax() {
        let executor = SkillExecutor::new();
        let input = "@greeting Alice";

        let result = executor.parse_invocation(input);
        assert!(result.is_ok());

        let (skill_name, params) = result.unwrap();
        assert_eq!(skill_name, "greeting");
        assert_eq!(params.get("__positional_0"), Some(&"Alice".to_string()));
    }

    #[test]
    fn test_execute_skill_shorthand_syntax() {
        let executor = SkillExecutor::new();
        let skill = create_test_skill();

        // 解析简写语法
        let (_, params) = executor.parse_invocation("@greeting Alice").unwrap();

        let result = executor.execute(&skill, params);
        assert!(result.is_ok());

        let prompt = result.unwrap();
        assert_eq!(prompt, "Hello Alice! Tone: friendly");
    }

    #[test]
    fn test_execute_skill_shorthand_with_spaces() {
        let executor = SkillExecutor::new();
        let skill = create_test_skill();

        let (_, params) = executor.parse_invocation("@greeting Alice Smith").unwrap();

        let result = executor.execute(&skill, params);
        assert!(result.is_ok());

        let prompt = result.unwrap();
        assert_eq!(prompt, "Hello Alice Smith! Tone: friendly");
    }
}
