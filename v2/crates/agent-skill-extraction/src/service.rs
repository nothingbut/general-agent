use std::path::PathBuf;
use std::sync::Arc;

use tokio::fs;
use tracing::{debug, info};
use uuid::Uuid;

use agent_core::models::message::Message;

use crate::error::{ExtractionError, Result};
use crate::extractor::SkillExtractorTrait;
use crate::models::{ExtractionRecord, ExtractionStats, ExtractionStatus, SkillDefinition};
use crate::repository::ExtractionRepository;

pub struct ExtractionService {
    extractor: Arc<dyn SkillExtractorTrait>,
    repository: ExtractionRepository,
    skills_dir: PathBuf,
}

impl ExtractionService {
    pub fn new(
        extractor: Arc<dyn SkillExtractorTrait>,
        repository: ExtractionRepository,
        skills_dir: PathBuf,
    ) -> Self {
        Self {
            extractor,
            repository,
            skills_dir,
        }
    }

    pub async fn extract_skill(
        &self,
        session_id: Uuid,
        messages: &[Message],
        hint: Option<&str>,
    ) -> Result<Option<SkillDefinition>> {
        let mut record = ExtractionRecord::new(session_id, messages.len() as i32);

        let extraction_result = match hint {
            Some(h) => self.extractor.extract_with_hint(messages, h).await,
            None => self.extractor.extract_from_messages(messages).await,
        };

        match extraction_result {
            Ok(Some(skill)) => {
                record = record.mark_success(
                    skill.name.clone(),
                    skill.namespace.clone(),
                );
                self.repository.save_record(&record).await?;
                info!("成功抽取技能: {}", skill.full_name());
                Ok(Some(skill))
            }
            Ok(None) => {
                record = record.mark_success("_none".to_string(), None);
                self.repository.save_record(&record).await?;
                debug!("未识别到可复用模式");
                Ok(None)
            }
            Err(e) => {
                record = record.mark_failed(e.to_string());
                self.repository.save_record(&record).await?;
                Err(e)
            }
        }
    }

    pub async fn save_skill(&self, skill: &SkillDefinition) -> Result<PathBuf> {
        let dir = match &skill.namespace {
            Some(ns) => {
                let ns_path = ns.replace(':', "/");
                self.skills_dir.join(&ns_path)
            }
            None => self.skills_dir.clone(),
        };

        fs::create_dir_all(&dir).await?;

        let file_path = dir.join(format!("{}.md", skill.name));

        if file_path.exists() {
            return Err(ExtractionError::SkillConflict(format!(
                "技能文件已存在: {}",
                file_path.display()
            )));
        }

        let content = skill.to_markdown();
        fs::write(&file_path, &content).await?;

        info!("技能已保存: {}", file_path.display());
        Ok(file_path)
    }

    pub async fn save_skill_force(&self, skill: &SkillDefinition) -> Result<PathBuf> {
        let dir = match &skill.namespace {
            Some(ns) => {
                let ns_path = ns.replace(':', "/");
                self.skills_dir.join(&ns_path)
            }
            None => self.skills_dir.clone(),
        };

        fs::create_dir_all(&dir).await?;

        let file_path = dir.join(format!("{}.md", skill.name));
        let content = skill.to_markdown();
        fs::write(&file_path, &content).await?;

        info!("技能已保存（覆盖）: {}", file_path.display());
        Ok(file_path)
    }

    pub async fn check_conflict(&self, skill: &SkillDefinition) -> Option<PathBuf> {
        let dir = match &skill.namespace {
            Some(ns) => {
                let ns_path = ns.replace(':', "/");
                self.skills_dir.join(&ns_path)
            }
            None => self.skills_dir.clone(),
        };

        let file_path = dir.join(format!("{}.md", skill.name));
        if file_path.exists() {
            Some(file_path)
        } else {
            None
        }
    }

    pub async fn extract_and_save(
        &self,
        session_id: Uuid,
        messages: &[Message],
        hint: Option<&str>,
        force: bool,
    ) -> Result<Option<PathBuf>> {
        let skill = self.extract_skill(session_id, messages, hint).await?;

        match skill {
            Some(s) => {
                let path = if force {
                    self.save_skill_force(&s).await?
                } else {
                    self.save_skill(&s).await?
                };
                Ok(Some(path))
            }
            None => Ok(None),
        }
    }

    pub async fn list_history(
        &self,
        status: Option<ExtractionStatus>,
        limit: u32,
    ) -> Result<Vec<ExtractionRecord>> {
        self.repository.list_records(status, limit).await
    }

    pub async fn get_record(&self, id: Uuid) -> Result<ExtractionRecord> {
        self.repository.get_record(id).await
    }

    pub async fn stats(&self) -> Result<ExtractionStats> {
        self.repository.stats().await
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use async_trait::async_trait;
    use sqlx::SqlitePool;
    use std::sync::atomic::{AtomicBool, Ordering};

    struct MockExtractor {
        return_skill: AtomicBool,
    }

    impl MockExtractor {
        fn new(return_skill: bool) -> Self {
            Self {
                return_skill: AtomicBool::new(return_skill),
            }
        }
    }

    #[async_trait]
    impl SkillExtractorTrait for MockExtractor {
        async fn extract_from_messages(&self, _messages: &[Message]) -> Result<Option<SkillDefinition>> {
            if self.return_skill.load(Ordering::Relaxed) {
                Ok(Some(SkillDefinition {
                    name: "test_skill".to_string(),
                    namespace: Some("testing".to_string()),
                    description: "测试技能".to_string(),
                    parameters: vec![],
                    template: "你好！".to_string(),
                }))
            } else {
                Ok(None)
            }
        }

        async fn extract_with_hint(&self, messages: &[Message], _hint: &str) -> Result<Option<SkillDefinition>> {
            self.extract_from_messages(messages).await
        }
    }

    async fn setup_service(return_skill: bool) -> (ExtractionService, tempfile::TempDir) {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        sqlx::migrate!("./migrations").run(&pool).await.unwrap();
        let repo = ExtractionRepository::new(pool);
        let skills_dir = tempfile::tempdir().unwrap();
        let extractor = Arc::new(MockExtractor::new(return_skill));
        let service = ExtractionService::new(extractor, repo, skills_dir.path().to_path_buf());
        (service, skills_dir)
    }

    fn test_messages(count: usize) -> Vec<Message> {
        use agent_core::models::message::MessageRole;
        let session_id = Uuid::new_v4();
        (0..count).map(|i| {
            let role = if i % 2 == 0 { MessageRole::User } else { MessageRole::Assistant };
            Message::new(session_id, role, format!("消息 {}", i))
        }).collect()
    }

    #[tokio::test]
    async fn test_extract_skill_success() {
        let (service, _dir) = setup_service(true).await;
        let messages = test_messages(6);
        let session_id = Uuid::new_v4();

        let result = service.extract_skill(session_id, &messages, None).await.unwrap();
        assert!(result.is_some());

        let skill = result.unwrap();
        assert_eq!(skill.name, "test_skill");
        assert_eq!(skill.namespace, Some("testing".to_string()));

        let stats = service.stats().await.unwrap();
        assert_eq!(stats.total_extractions, 1);
        assert_eq!(stats.successful, 1);
    }

    #[tokio::test]
    async fn test_extract_skill_no_pattern() {
        let (service, _dir) = setup_service(false).await;
        let messages = test_messages(6);
        let session_id = Uuid::new_v4();

        let result = service.extract_skill(session_id, &messages, None).await.unwrap();
        assert!(result.is_none());

        let stats = service.stats().await.unwrap();
        assert_eq!(stats.total_extractions, 1);
    }

    #[tokio::test]
    async fn test_save_skill() {
        let (service, dir) = setup_service(true).await;
        let skill = SkillDefinition {
            name: "greet".to_string(),
            namespace: Some("personal".to_string()),
            description: "问候".to_string(),
            parameters: vec![],
            template: "你好！".to_string(),
        };

        let path = service.save_skill(&skill).await.unwrap();
        assert!(path.exists());
        assert!(path.ends_with("personal/greet.md"));

        let content = fs::read_to_string(&path).await.unwrap();
        assert!(content.contains("name: greet"));
    }

    #[tokio::test]
    async fn test_save_skill_conflict() {
        let (service, _dir) = setup_service(true).await;
        let skill = SkillDefinition {
            name: "greet".to_string(),
            namespace: None,
            description: "问候".to_string(),
            parameters: vec![],
            template: "你好！".to_string(),
        };

        service.save_skill(&skill).await.unwrap();
        let result = service.save_skill(&skill).await;
        assert!(matches!(result, Err(ExtractionError::SkillConflict(_))));
    }

    #[tokio::test]
    async fn test_save_skill_force_overwrite() {
        let (service, _dir) = setup_service(true).await;
        let skill = SkillDefinition {
            name: "greet".to_string(),
            namespace: None,
            description: "问候".to_string(),
            parameters: vec![],
            template: "你好！".to_string(),
        };

        service.save_skill(&skill).await.unwrap();
        let path = service.save_skill_force(&skill).await.unwrap();
        assert!(path.exists());
    }

    #[tokio::test]
    async fn test_check_conflict() {
        let (service, _dir) = setup_service(true).await;
        let skill = SkillDefinition {
            name: "greet".to_string(),
            namespace: None,
            description: "问候".to_string(),
            parameters: vec![],
            template: "你好！".to_string(),
        };

        assert!(service.check_conflict(&skill).await.is_none());
        service.save_skill(&skill).await.unwrap();
        assert!(service.check_conflict(&skill).await.is_some());
    }

    #[tokio::test]
    async fn test_extract_and_save() {
        let (service, _dir) = setup_service(true).await;
        let messages = test_messages(6);
        let session_id = Uuid::new_v4();

        let path = service.extract_and_save(session_id, &messages, None, false).await.unwrap();
        assert!(path.is_some());
        assert!(path.unwrap().exists());
    }

    #[tokio::test]
    async fn test_list_history() {
        let (service, _dir) = setup_service(true).await;
        let messages = test_messages(6);

        service.extract_skill(Uuid::new_v4(), &messages, None).await.unwrap();
        service.extract_skill(Uuid::new_v4(), &messages, None).await.unwrap();

        let history = service.list_history(None, 10).await.unwrap();
        assert_eq!(history.len(), 2);

        let success = service.list_history(Some(ExtractionStatus::Success), 10).await.unwrap();
        assert_eq!(success.len(), 2);
    }
}
