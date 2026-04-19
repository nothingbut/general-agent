use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::fmt;
use uuid::Uuid;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SkillDefinition {
    pub name: String,
    pub namespace: Option<String>,
    pub description: String,
    pub parameters: Vec<SkillParameter>,
    pub template: String,
}

impl SkillDefinition {
    pub fn full_name(&self) -> String {
        match &self.namespace {
            Some(ns) => format!("{}:{}", ns, self.name),
            None => self.name.clone(),
        }
    }

    pub fn to_markdown(&self) -> String {
        let mut out = String::new();
        out.push_str("---\n");
        out.push_str(&format!("name: {}\n", self.name));
        out.push_str(&format!("description: {}\n", self.description));
        if !self.parameters.is_empty() {
            out.push_str("parameters:\n");
            for p in &self.parameters {
                out.push_str(&format!("  - name: {}\n", p.name));
                out.push_str(&format!("    type: {}\n", p.param_type));
                out.push_str(&format!("    required: {}\n", p.required));
                out.push_str(&format!("    description: {}\n", p.description));
                if let Some(ref default) = p.default_value {
                    out.push_str(&format!("    default: {}\n", default));
                }
            }
        }
        out.push_str("---\n\n");
        out.push_str(&self.template);
        out
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SkillParameter {
    pub name: String,
    pub param_type: String,
    pub required: bool,
    pub description: String,
    pub default_value: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
pub enum ExtractionStatus {
    Success,
    Failed,
    Pending,
}

impl fmt::Display for ExtractionStatus {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ExtractionStatus::Success => write!(f, "success"),
            ExtractionStatus::Failed => write!(f, "failed"),
            ExtractionStatus::Pending => write!(f, "pending"),
        }
    }
}

impl ExtractionStatus {
    pub fn from_str(s: &str) -> Option<Self> {
        match s.to_lowercase().as_str() {
            "success" => Some(ExtractionStatus::Success),
            "failed" => Some(ExtractionStatus::Failed),
            "pending" => Some(ExtractionStatus::Pending),
            _ => None,
        }
    }

    pub fn as_str(&self) -> &'static str {
        match self {
            ExtractionStatus::Success => "success",
            ExtractionStatus::Failed => "failed",
            ExtractionStatus::Pending => "pending",
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ExtractionRecord {
    pub id: Uuid,
    pub session_id: Uuid,
    pub extracted_at: DateTime<Utc>,
    pub status: ExtractionStatus,
    pub skill_name: Option<String>,
    pub skill_namespace: Option<String>,
    pub message_count: i32,
    pub error_message: Option<String>,
}

impl ExtractionRecord {
    pub fn new(session_id: Uuid, message_count: i32) -> Self {
        Self {
            id: Uuid::new_v4(),
            session_id,
            extracted_at: Utc::now(),
            status: ExtractionStatus::Pending,
            skill_name: None,
            skill_namespace: None,
            message_count,
            error_message: None,
        }
    }

    pub fn mark_success(mut self, skill_name: String, namespace: Option<String>) -> Self {
        self.status = ExtractionStatus::Success;
        self.skill_name = Some(skill_name);
        self.skill_namespace = namespace;
        self
    }

    pub fn mark_failed(mut self, error: String) -> Self {
        self.status = ExtractionStatus::Failed;
        self.error_message = Some(error);
        self
    }
}

#[derive(Debug, Clone, Default)]
pub struct ExtractionStats {
    pub total_extractions: i64,
    pub successful: i64,
    pub failed: i64,
    pub unique_skills: i64,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_skill_definition_full_name() {
        let skill = SkillDefinition {
            name: "greeting".to_string(),
            namespace: Some("personal".to_string()),
            description: "问候".to_string(),
            parameters: vec![],
            template: "你好！".to_string(),
        };
        assert_eq!(skill.full_name(), "personal:greeting");

        let no_ns = SkillDefinition {
            name: "hello".to_string(),
            namespace: None,
            description: "Hello".to_string(),
            parameters: vec![],
            template: "Hi".to_string(),
        };
        assert_eq!(no_ns.full_name(), "hello");
    }

    #[test]
    fn test_skill_definition_to_markdown() {
        let skill = SkillDefinition {
            name: "greet".to_string(),
            namespace: None,
            description: "向用户问候".to_string(),
            parameters: vec![SkillParameter {
                name: "user_name".to_string(),
                param_type: "string".to_string(),
                required: true,
                description: "用户名称".to_string(),
                default_value: None,
            }],
            template: "你好 {{ user_name }}！".to_string(),
        };

        let md = skill.to_markdown();
        assert!(md.contains("name: greet"));
        assert!(md.contains("description: 向用户问候"));
        assert!(md.contains("- name: user_name"));
        assert!(md.contains("你好 {{ user_name }}！"));
    }

    #[test]
    fn test_extraction_status() {
        assert_eq!(ExtractionStatus::from_str("success"), Some(ExtractionStatus::Success));
        assert_eq!(ExtractionStatus::from_str("FAILED"), Some(ExtractionStatus::Failed));
        assert_eq!(ExtractionStatus::from_str("unknown"), None);
        assert_eq!(ExtractionStatus::Success.to_string(), "success");
    }

    #[test]
    fn test_extraction_record_lifecycle() {
        let record = ExtractionRecord::new(Uuid::new_v4(), 10);
        assert_eq!(record.status, ExtractionStatus::Pending);

        let success = record.clone().mark_success("greet".to_string(), Some("personal".to_string()));
        assert_eq!(success.status, ExtractionStatus::Success);
        assert_eq!(success.skill_name, Some("greet".to_string()));

        let failed = record.mark_failed("LLM error".to_string());
        assert_eq!(failed.status, ExtractionStatus::Failed);
        assert_eq!(failed.error_message, Some("LLM error".to_string()));
    }
}
