use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::fmt;
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum MemoryType {
    User,
    Feedback,
    Project,
    Reference,
    Knowledge,
}

impl MemoryType {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::User => "user",
            Self::Feedback => "feedback",
            Self::Project => "project",
            Self::Reference => "reference",
            Self::Knowledge => "knowledge",
        }
    }

    pub fn from_str(s: &str) -> Option<Self> {
        match s.to_lowercase().as_str() {
            "user" => Some(Self::User),
            "feedback" => Some(Self::Feedback),
            "project" => Some(Self::Project),
            "reference" => Some(Self::Reference),
            "knowledge" => Some(Self::Knowledge),
            _ => None,
        }
    }

    pub fn all() -> &'static [MemoryType] {
        &[
            Self::User,
            Self::Feedback,
            Self::Project,
            Self::Reference,
            Self::Knowledge,
        ]
    }
}

impl fmt::Display for MemoryType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.as_str())
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Memory {
    pub id: Uuid,
    pub memory_type: MemoryType,
    pub content: String,
    pub source: Option<String>,
    pub session_id: Option<Uuid>,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub metadata: Option<serde_json::Value>,
}

impl Memory {
    pub fn new(memory_type: MemoryType, content: String) -> Self {
        let now = Utc::now();
        Self {
            id: Uuid::new_v4(),
            memory_type,
            content,
            source: None,
            session_id: None,
            created_at: now,
            updated_at: now,
            metadata: None,
        }
    }

    pub fn with_source(mut self, source: String) -> Self {
        self.source = Some(source);
        self
    }

    pub fn with_session(mut self, session_id: Uuid) -> Self {
        self.session_id = Some(session_id);
        self
    }

    pub fn with_metadata(mut self, metadata: serde_json::Value) -> Self {
        self.metadata = Some(metadata);
        self
    }
}

#[derive(Debug, Clone, Default)]
pub struct MemoryQuery {
    pub memory_type: Option<MemoryType>,
    pub session_id: Option<Uuid>,
    pub keyword: Option<String>,
    pub limit: Option<u32>,
    pub offset: Option<u32>,
}

impl MemoryQuery {
    pub fn by_type(memory_type: MemoryType) -> Self {
        Self {
            memory_type: Some(memory_type),
            ..Default::default()
        }
    }

    pub fn by_session(session_id: Uuid) -> Self {
        Self {
            session_id: Some(session_id),
            ..Default::default()
        }
    }

    pub fn with_keyword(mut self, keyword: String) -> Self {
        self.keyword = Some(keyword);
        self
    }

    pub fn with_limit(mut self, limit: u32) -> Self {
        self.limit = Some(limit);
        self
    }

    pub fn with_offset(mut self, offset: u32) -> Self {
        self.offset = Some(offset);
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_memory_type_roundtrip() {
        for mt in MemoryType::all() {
            let s = mt.as_str();
            let parsed = MemoryType::from_str(s).unwrap();
            assert_eq!(*mt, parsed);
        }
    }

    #[test]
    fn test_memory_type_invalid() {
        assert!(MemoryType::from_str("invalid").is_none());
        assert!(MemoryType::from_str("").is_none());
    }

    #[test]
    fn test_memory_type_case_insensitive() {
        assert_eq!(MemoryType::from_str("USER"), Some(MemoryType::User));
        assert_eq!(MemoryType::from_str("Feedback"), Some(MemoryType::Feedback));
    }

    #[test]
    fn test_memory_new() {
        let memory = Memory::new(MemoryType::User, "test content".to_string());
        assert_eq!(memory.memory_type, MemoryType::User);
        assert_eq!(memory.content, "test content");
        assert!(memory.source.is_none());
        assert!(memory.session_id.is_none());
        assert!(memory.metadata.is_none());
    }

    #[test]
    fn test_memory_builder() {
        let session_id = Uuid::new_v4();
        let memory = Memory::new(MemoryType::Feedback, "rule".to_string())
            .with_source("conversation".to_string())
            .with_session(session_id)
            .with_metadata(serde_json::json!({"key": "value"}));

        assert_eq!(memory.memory_type, MemoryType::Feedback);
        assert_eq!(memory.source.as_deref(), Some("conversation"));
        assert_eq!(memory.session_id, Some(session_id));
        assert!(memory.metadata.is_some());
    }

    #[test]
    fn test_memory_serialization() {
        let memory = Memory::new(MemoryType::Project, "deadline info".to_string());
        let json = serde_json::to_string(&memory).unwrap();
        let deserialized: Memory = serde_json::from_str(&json).unwrap();
        assert_eq!(deserialized.content, "deadline info");
        assert_eq!(deserialized.memory_type, MemoryType::Project);
    }

    #[test]
    fn test_memory_query_builders() {
        let query = MemoryQuery::by_type(MemoryType::User)
            .with_keyword("test".to_string())
            .with_limit(10)
            .with_offset(5);

        assert_eq!(query.memory_type, Some(MemoryType::User));
        assert_eq!(query.keyword.as_deref(), Some("test"));
        assert_eq!(query.limit, Some(10));
        assert_eq!(query.offset, Some(5));
    }

    #[test]
    fn test_memory_type_display() {
        assert_eq!(format!("{}", MemoryType::User), "user");
        assert_eq!(format!("{}", MemoryType::Knowledge), "knowledge");
    }

    #[test]
    fn test_memory_all_types() {
        let all = MemoryType::all();
        assert_eq!(all.len(), 5);
    }
}
