//! 技能系统数据模型

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::path::PathBuf;

/// 技能参数定义
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct SkillParameter {
    /// 参数名称
    pub name: String,

    /// 参数类型 (string, number, boolean, etc.)
    #[serde(rename = "type")]
    pub param_type: String,

    /// 是否必需
    pub required: bool,

    /// 参数描述
    pub description: String,

    /// 默认值（仅用于可选参数）
    #[serde(skip_serializing_if = "Option::is_none")]
    pub default: Option<String>,
}

impl SkillParameter {
    /// 创建新的参数定义
    pub fn new(name: String, param_type: String, required: bool, description: String) -> Self {
        Self {
            name,
            param_type,
            required,
            description,
            default: None,
        }
    }

    /// 设置默认值
    pub fn with_default(mut self, default: String) -> Self {
        self.default = Some(default);
        self
    }
}

/// 技能定义
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SkillDefinition {
    /// 技能短名称
    pub name: String,

    /// 技能描述
    pub description: String,

    /// 命名空间（从文件路径提取）
    #[serde(skip)]
    pub namespace: String,

    /// 参数列表
    #[serde(default)]
    pub parameters: Vec<SkillParameter>,

    /// Markdown 内容（提示词模板）
    #[serde(skip)]
    pub content: String,

    /// 文件路径
    #[serde(skip)]
    pub file_path: PathBuf,
}

impl SkillDefinition {
    /// 创建新的技能定义
    pub fn new(name: String, description: String) -> Self {
        Self {
            name,
            description,
            namespace: String::new(),
            parameters: Vec::new(),
            content: String::new(),
            file_path: PathBuf::new(),
        }
    }

    /// 获取完整名称 (namespace:name)
    pub fn full_name(&self) -> String {
        if self.namespace.is_empty() {
            self.name.clone()
        } else {
            format!("{}:{}", self.namespace, self.name)
        }
    }

    /// 检查是否有必需参数
    pub fn has_required_parameters(&self) -> bool {
        self.parameters.iter().any(|p| p.required)
    }

    /// 获取所有必需参数的名称
    pub fn required_parameter_names(&self) -> Vec<String> {
        self.parameters
            .iter()
            .filter(|p| p.required)
            .map(|p| p.name.clone())
            .collect()
    }

    /// 获取参数定义
    pub fn get_parameter(&self, name: &str) -> Option<&SkillParameter> {
        self.parameters.iter().find(|p| p.name == name)
    }
}

/// 技能执行上下文
#[derive(Debug, Clone)]
pub struct SkillExecutionContext {
    /// 技能定义
    pub skill: SkillDefinition,

    /// 提供的参数值
    pub parameters: HashMap<String, String>,
}

impl SkillExecutionContext {
    /// 创建新的执行上下文
    pub fn new(skill: SkillDefinition, parameters: HashMap<String, String>) -> Self {
        Self { skill, parameters }
    }

    /// 验证参数
    pub fn validate(&self) -> Result<(), String> {
        // 检查所有必需参数
        for param in &self.skill.parameters {
            if param.required && !self.parameters.contains_key(&param.name) {
                return Err(format!("Required parameter '{}' is missing", param.name));
            }

            // 检查参数值非空
            if let Some(value) = self.parameters.get(&param.name) {
                if value.trim().is_empty() {
                    return Err(format!("Parameter '{}' cannot be empty", param.name));
                }
            }
        }

        Ok(())
    }

    /// 获取参数值（包括默认值）
    pub fn get_parameter_value(&self, name: &str) -> Option<String> {
        // 首先尝试从提供的参数中获取
        if let Some(value) = self.parameters.get(name) {
            return Some(value.clone());
        }

        // 尝试获取默认值
        if let Some(param) = self.skill.get_parameter(name) {
            return param.default.clone();
        }

        None
    }

    /// 构建提示词（替换占位符）
    pub fn build_prompt(&self) -> String {
        let mut prompt = self.skill.content.clone();

        // 获取所有参数值（包括默认值）
        for param in &self.skill.parameters {
            if let Some(value) = self.get_parameter_value(&param.name) {
                let placeholder = format!("{{{}}}", param.name);
                prompt = prompt.replace(&placeholder, &value);
            }
        }

        prompt
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_skill_parameter_creation() {
        let param = SkillParameter::new(
            "user_name".to_string(),
            "string".to_string(),
            true,
            "User's name".to_string(),
        );

        assert_eq!(param.name, "user_name");
        assert_eq!(param.param_type, "string");
        assert!(param.required);
        assert_eq!(param.description, "User's name");
        assert!(param.default.is_none());
    }

    #[test]
    fn test_skill_parameter_with_default() {
        let param = SkillParameter::new(
            "tone".to_string(),
            "string".to_string(),
            false,
            "Greeting tone".to_string(),
        )
        .with_default("friendly".to_string());

        assert!(!param.required);
        assert_eq!(param.default, Some("friendly".to_string()));
    }

    #[test]
    fn test_skill_definition_creation() {
        let skill = SkillDefinition::new("greeting".to_string(), "Greet the user".to_string());

        assert_eq!(skill.name, "greeting");
        assert_eq!(skill.description, "Greet the user");
        assert!(skill.namespace.is_empty());
        assert!(skill.parameters.is_empty());
    }

    #[test]
    fn test_skill_full_name_without_namespace() {
        let skill = SkillDefinition::new("greeting".to_string(), "Greet the user".to_string());
        assert_eq!(skill.full_name(), "greeting");
    }

    #[test]
    fn test_skill_full_name_with_namespace() {
        let mut skill = SkillDefinition::new("greeting".to_string(), "Greet the user".to_string());
        skill.namespace = "personal".to_string();
        assert_eq!(skill.full_name(), "personal:greeting");
    }

    #[test]
    fn test_skill_has_required_parameters() {
        let mut skill = SkillDefinition::new("test".to_string(), "Test".to_string());

        assert!(!skill.has_required_parameters());

        skill.parameters.push(SkillParameter::new(
            "optional".to_string(),
            "string".to_string(),
            false,
            "Optional param".to_string(),
        ));
        assert!(!skill.has_required_parameters());

        skill.parameters.push(SkillParameter::new(
            "required".to_string(),
            "string".to_string(),
            true,
            "Required param".to_string(),
        ));
        assert!(skill.has_required_parameters());
    }

    #[test]
    fn test_skill_required_parameter_names() {
        let mut skill = SkillDefinition::new("test".to_string(), "Test".to_string());

        skill.parameters.push(SkillParameter::new(
            "param1".to_string(),
            "string".to_string(),
            true,
            "Param 1".to_string(),
        ));
        skill.parameters.push(SkillParameter::new(
            "param2".to_string(),
            "string".to_string(),
            false,
            "Param 2".to_string(),
        ));
        skill.parameters.push(SkillParameter::new(
            "param3".to_string(),
            "string".to_string(),
            true,
            "Param 3".to_string(),
        ));

        let required = skill.required_parameter_names();
        assert_eq!(required.len(), 2);
        assert!(required.contains(&"param1".to_string()));
        assert!(required.contains(&"param3".to_string()));
    }

    #[test]
    fn test_execution_context_validate_success() {
        let mut skill = SkillDefinition::new("test".to_string(), "Test".to_string());
        skill.parameters.push(SkillParameter::new(
            "name".to_string(),
            "string".to_string(),
            true,
            "Name".to_string(),
        ));

        let mut params = HashMap::new();
        params.insert("name".to_string(), "Alice".to_string());

        let context = SkillExecutionContext::new(skill, params);
        assert!(context.validate().is_ok());
    }

    #[test]
    fn test_execution_context_validate_missing_required() {
        let mut skill = SkillDefinition::new("test".to_string(), "Test".to_string());
        skill.parameters.push(SkillParameter::new(
            "name".to_string(),
            "string".to_string(),
            true,
            "Name".to_string(),
        ));

        let params = HashMap::new();
        let context = SkillExecutionContext::new(skill, params);

        let result = context.validate();
        assert!(result.is_err());
        assert!(result
            .unwrap_err()
            .contains("Required parameter 'name' is missing"));
    }

    #[test]
    fn test_execution_context_validate_empty_value() {
        let mut skill = SkillDefinition::new("test".to_string(), "Test".to_string());
        skill.parameters.push(SkillParameter::new(
            "name".to_string(),
            "string".to_string(),
            true,
            "Name".to_string(),
        ));

        let mut params = HashMap::new();
        params.insert("name".to_string(), "   ".to_string());

        let context = SkillExecutionContext::new(skill, params);
        let result = context.validate();
        assert!(result.is_err());
        assert!(result.unwrap_err().contains("cannot be empty"));
    }

    #[test]
    fn test_execution_context_get_parameter_value() {
        let mut skill = SkillDefinition::new("test".to_string(), "Test".to_string());
        skill.parameters.push(
            SkillParameter::new(
                "tone".to_string(),
                "string".to_string(),
                false,
                "Tone".to_string(),
            )
            .with_default("friendly".to_string()),
        );

        let params = HashMap::new();
        let context = SkillExecutionContext::new(skill, params);

        // 获取默认值
        assert_eq!(
            context.get_parameter_value("tone"),
            Some("friendly".to_string())
        );

        // 不存在的参数
        assert_eq!(context.get_parameter_value("nonexistent"), None);
    }

    #[test]
    fn test_execution_context_build_prompt() {
        let mut skill = SkillDefinition::new("greeting".to_string(), "Greet user".to_string());
        skill.content = "Hello {user_name}! Tone: {tone}".to_string();
        skill.parameters.push(SkillParameter::new(
            "user_name".to_string(),
            "string".to_string(),
            true,
            "Name".to_string(),
        ));
        skill.parameters.push(
            SkillParameter::new(
                "tone".to_string(),
                "string".to_string(),
                false,
                "Tone".to_string(),
            )
            .with_default("friendly".to_string()),
        );

        let mut params = HashMap::new();
        params.insert("user_name".to_string(), "Alice".to_string());

        let context = SkillExecutionContext::new(skill, params);
        let prompt = context.build_prompt();

        assert_eq!(prompt, "Hello Alice! Tone: friendly");
    }

    #[test]
    fn test_execution_context_build_prompt_override_default() {
        let mut skill = SkillDefinition::new("greeting".to_string(), "Greet user".to_string());
        skill.content = "Hello {user_name}! Tone: {tone}".to_string();
        skill.parameters.push(SkillParameter::new(
            "user_name".to_string(),
            "string".to_string(),
            true,
            "Name".to_string(),
        ));
        skill.parameters.push(
            SkillParameter::new(
                "tone".to_string(),
                "string".to_string(),
                false,
                "Tone".to_string(),
            )
            .with_default("friendly".to_string()),
        );

        let mut params = HashMap::new();
        params.insert("user_name".to_string(), "Bob".to_string());
        params.insert("tone".to_string(), "professional".to_string());

        let context = SkillExecutionContext::new(skill, params);
        let prompt = context.build_prompt();

        assert_eq!(prompt, "Hello Bob! Tone: professional");
    }
}
