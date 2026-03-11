//! 技能注册表
//!
//! 存储和管理已加载的技能定义

use crate::models::SkillDefinition;
use std::collections::HashMap;
use thiserror::Error;

/// 注册表错误类型
#[derive(Debug, Error)]
pub enum RegistryError {
    #[error("Skill not found: {0}")]
    SkillNotFound(String),

    #[error("Ambiguous skill name '{name}'. Multiple skills found: {}", candidates.join(", "))]
    AmbiguousSkillName {
        name: String,
        candidates: Vec<String>,
    },
}

pub type Result<T> = std::result::Result<T, RegistryError>;

/// 技能注册表
#[derive(Debug, Default)]
pub struct SkillRegistry {
    // 完整名称 -> 技能定义
    skills: HashMap<String, SkillDefinition>,

    // 短名称 -> 完整名称列表（用于检测歧义）
    short_name_index: HashMap<String, Vec<String>>,
}

impl SkillRegistry {
    /// 创建新的注册表
    pub fn new() -> Self {
        Self {
            skills: HashMap::new(),
            short_name_index: HashMap::new(),
        }
    }

    /// 注册技能
    pub fn register(&mut self, skill: SkillDefinition) {
        let full_name = skill.full_name();
        let short_name = skill.name.clone();

        // 存储完整定义
        self.skills.insert(full_name.clone(), skill);

        // 更新短名称索引
        self.short_name_index
            .entry(short_name)
            .or_default()
            .push(full_name);
    }

    /// 获取技能（支持短名称和完整名称）
    pub fn get(&self, name: &str) -> Result<&SkillDefinition> {
        // 1. 尝试作为完整名称查找
        if let Some(skill) = self.skills.get(name) {
            return Ok(skill);
        }

        // 2. 尝试作为短名称查找
        if let Some(full_names) = self.short_name_index.get(name) {
            match full_names.len() {
                0 => Err(RegistryError::SkillNotFound(name.to_string())),
                1 => Ok(&self.skills[&full_names[0]]),
                _ => Err(RegistryError::AmbiguousSkillName {
                    name: name.to_string(),
                    candidates: full_names.clone(),
                }),
            }
        } else {
            Err(RegistryError::SkillNotFound(name.to_string()))
        }
    }

    /// 列出所有技能
    pub fn list_all(&self) -> Vec<&SkillDefinition> {
        self.skills.values().collect()
    }

    /// 按命名空间查询
    pub fn list_by_namespace(&self, namespace: &str) -> Vec<&SkillDefinition> {
        self.skills
            .values()
            .filter(|s| s.namespace == namespace)
            .collect()
    }

    /// 获取注册技能的数量
    pub fn count(&self) -> usize {
        self.skills.len()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // 测试辅助函数：创建测试技能
    fn create_test_skill(name: &str, namespace: &str) -> SkillDefinition {
        let mut skill = SkillDefinition::new(name.to_string(), format!("{} skill", name));
        skill.namespace = namespace.to_string();
        skill.content = "Test content".to_string();
        skill
    }

    #[test]
    fn test_register_and_get_by_full_name() {
        let mut registry = SkillRegistry::new();
        let skill = create_test_skill("greeting", "personal");

        registry.register(skill);

        let result = registry.get("personal:greeting");
        assert!(result.is_ok());
        let retrieved = result.unwrap();
        assert_eq!(retrieved.name, "greeting");
        assert_eq!(retrieved.namespace, "personal");
    }

    #[test]
    fn test_register_and_get_by_short_name() {
        let mut registry = SkillRegistry::new();
        let skill = create_test_skill("greeting", "personal");

        registry.register(skill);

        // 可以通过短名称访问（没有歧义）
        let result = registry.get("greeting");
        assert!(result.is_ok());
        let retrieved = result.unwrap();
        assert_eq!(retrieved.name, "greeting");
    }

    #[test]
    fn test_get_nonexistent_skill() {
        let registry = SkillRegistry::new();

        let result = registry.get("nonexistent");
        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            RegistryError::SkillNotFound(_)
        ));
    }

    #[test]
    fn test_ambiguous_skill_name() {
        let mut registry = SkillRegistry::new();

        // 注册两个同名但不同命名空间的技能
        let skill1 = create_test_skill("greeting", "personal");
        let skill2 = create_test_skill("greeting", "work");

        registry.register(skill1);
        registry.register(skill2);

        // 通过完整名称可以访问
        assert!(registry.get("personal:greeting").is_ok());
        assert!(registry.get("work:greeting").is_ok());

        // 通过短名称会产生歧义
        let result = registry.get("greeting");
        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            RegistryError::AmbiguousSkillName { .. }
        ));
    }

    #[test]
    fn test_list_all() {
        let mut registry = SkillRegistry::new();

        let skill1 = create_test_skill("greeting", "personal");
        let skill2 = create_test_skill("farewell", "personal");
        let skill3 = create_test_skill("meeting", "work");

        registry.register(skill1);
        registry.register(skill2);
        registry.register(skill3);

        let all_skills = registry.list_all();
        assert_eq!(all_skills.len(), 3);

        let names: Vec<&str> = all_skills.iter().map(|s| s.name.as_str()).collect();
        assert!(names.contains(&"greeting"));
        assert!(names.contains(&"farewell"));
        assert!(names.contains(&"meeting"));
    }

    #[test]
    fn test_list_by_namespace() {
        let mut registry = SkillRegistry::new();

        let skill1 = create_test_skill("greeting", "personal");
        let skill2 = create_test_skill("farewell", "personal");
        let skill3 = create_test_skill("meeting", "work");

        registry.register(skill1);
        registry.register(skill2);
        registry.register(skill3);

        let personal_skills = registry.list_by_namespace("personal");
        assert_eq!(personal_skills.len(), 2);

        let work_skills = registry.list_by_namespace("work");
        assert_eq!(work_skills.len(), 1);

        let nonexistent = registry.list_by_namespace("nonexistent");
        assert_eq!(nonexistent.len(), 0);
    }

    #[test]
    fn test_count() {
        let mut registry = SkillRegistry::new();
        assert_eq!(registry.count(), 0);

        registry.register(create_test_skill("greeting", "personal"));
        assert_eq!(registry.count(), 1);

        registry.register(create_test_skill("farewell", "personal"));
        assert_eq!(registry.count(), 2);
    }

    #[test]
    fn test_register_skill_without_namespace() {
        let mut registry = SkillRegistry::new();
        let mut skill = SkillDefinition::new("greeting".to_string(), "A greeting".to_string());
        skill.namespace = String::new(); // 空命名空间

        registry.register(skill);

        // 应该可以通过短名称访问
        let result = registry.get("greeting");
        assert!(result.is_ok());
        assert_eq!(result.unwrap().full_name(), "greeting");
    }

    #[test]
    fn test_overwrite_existing_skill() {
        let mut registry = SkillRegistry::new();

        let skill1 = create_test_skill("greeting", "personal");
        registry.register(skill1);

        // 注册同名技能（覆盖）
        let mut skill2 = create_test_skill("greeting", "personal");
        skill2.description = "Updated description".to_string();
        registry.register(skill2);

        // 应该得到最新的版本
        let retrieved = registry.get("personal:greeting").unwrap();
        assert_eq!(retrieved.description, "Updated description");

        // 总数仍然是 1
        assert_eq!(registry.count(), 1);
    }

    #[test]
    fn test_nested_namespace() {
        let mut registry = SkillRegistry::new();
        let mut skill = create_test_skill("reply", "work");
        skill.namespace = "work:email".to_string();

        registry.register(skill);

        let result = registry.get("work:email:reply");
        assert!(result.is_ok());
        assert_eq!(result.unwrap().namespace, "work:email");
    }
}
