//! 技能文件加载器
//!
//! 负责从文件系统加载技能文件

use crate::models::SkillDefinition;
use crate::parser::{ParseError, SkillParser};
use ignore::gitignore::Gitignore;
use std::path::{Path, PathBuf};
use thiserror::Error;
use walkdir::WalkDir;

/// 加载器错误类型
#[derive(Debug, Error)]
pub enum LoadError {
    #[error("IO error: {0}")]
    IoError(#[from] std::io::Error),

    #[error("Parse error: {0}")]
    ParseError(#[from] ParseError),

    #[error("Invalid skills directory: {0}")]
    InvalidDirectory(String),

    #[error("WalkDir error: {0}")]
    WalkDirError(#[from] walkdir::Error),
}

pub type Result<T> = std::result::Result<T, LoadError>;

/// 技能文件加载器
#[derive(Debug)]
pub struct SkillLoader {
    skills_dir: PathBuf,
    ignore_patterns: Gitignore,
}

impl SkillLoader {
    /// 创建新的加载器
    pub fn new(skills_dir: PathBuf) -> Result<Self> {
        if !skills_dir.exists() {
            return Err(LoadError::InvalidDirectory(format!(
                "Directory does not exist: {}",
                skills_dir.display()
            )));
        }

        if !skills_dir.is_dir() {
            return Err(LoadError::InvalidDirectory(format!(
                "Path is not a directory: {}",
                skills_dir.display()
            )));
        }

        let ignore_patterns = Self::load_ignore_patterns(&skills_dir);
        Ok(Self {
            skills_dir,
            ignore_patterns,
        })
    }

    /// 加载所有技能
    pub fn load_all(&self) -> Result<Vec<SkillDefinition>> {
        let mut skills = Vec::new();

        for entry in WalkDir::new(&self.skills_dir)
            .into_iter()
            .filter_entry(|e| {
                // 过滤掉被忽略的目录和文件
                !self.should_ignore(e.path())
            })
        {
            let entry = entry?;
            let path = entry.path();

            // 跳过目录
            if !path.is_file() {
                continue;
            }

            // 只处理 .md 文件
            if path.extension().and_then(|s| s.to_str()) != Some("md") {
                continue;
            }

            // 读取并解析技能文件
            let content = std::fs::read_to_string(path)?;
            let mut skill = SkillParser::parse(&content, path)?;

            // 提取并设置命名空间
            skill.namespace = self.extract_namespace(path)?;

            skills.push(skill);
        }

        Ok(skills)
    }

    /// 提取命名空间
    /// 从文件路径提取命名空间
    /// 例如: skills/personal/greeting.md -> "personal"
    ///      skills/work/email/reply.md -> "work:email"
    fn extract_namespace(&self, path: &Path) -> Result<String> {
        let relative = path
            .strip_prefix(&self.skills_dir)
            .map_err(|e| LoadError::InvalidDirectory(e.to_string()))?;

        let parent = relative.parent().unwrap_or(Path::new(""));

        if parent.as_os_str().is_empty() {
            Ok(String::new())
        } else {
            Ok(parent
                .to_string_lossy()
                .replace(std::path::MAIN_SEPARATOR, ":"))
        }
    }

    /// 检查文件是否应该被忽略
    fn should_ignore(&self, path: &Path) -> bool {
        // 需要使用相对路径进行匹配
        if let Ok(relative) = path.strip_prefix(&self.skills_dir) {
            self.ignore_patterns
                .matched(relative, path.is_dir())
                .is_ignore()
        } else {
            false
        }
    }

    /// 加载 .ignore 文件
    fn load_ignore_patterns(skills_dir: &Path) -> Gitignore {
        let ignore_path = skills_dir.join(".ignore");
        if ignore_path.exists() {
            let mut builder = ignore::gitignore::GitignoreBuilder::new(skills_dir);
            let _ = builder.add(&ignore_path);
            builder.build().unwrap_or_else(|_| Gitignore::empty())
        } else {
            Gitignore::empty()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;

    // 测试辅助函数：创建临时测试目录
    fn setup_test_dir() -> tempfile::TempDir {
        tempfile::tempdir().unwrap()
    }

    // 测试辅助函数：在目录中创建技能文件
    fn create_skill_file(dir: &Path, relative_path: &str, content: &str) {
        let file_path = dir.join(relative_path);
        if let Some(parent) = file_path.parent() {
            fs::create_dir_all(parent).unwrap();
        }
        fs::write(&file_path, content).unwrap();
    }

    #[test]
    fn test_new_with_valid_directory() {
        let temp_dir = setup_test_dir();
        let result = SkillLoader::new(temp_dir.path().to_path_buf());
        assert!(result.is_ok());
    }

    #[test]
    fn test_new_with_nonexistent_directory() {
        let nonexistent = PathBuf::from("/path/that/does/not/exist");
        let result = SkillLoader::new(nonexistent);
        assert!(result.is_err());
        assert!(matches!(
            result.unwrap_err(),
            LoadError::InvalidDirectory(_)
        ));
    }

    #[test]
    fn test_load_single_skill_file() {
        let temp_dir = setup_test_dir();

        // 创建一个简单的技能文件
        let skill_content = r#"---
name: greeting
description: A simple greeting skill
---

# Greeting

Hello, World!
"#;
        create_skill_file(temp_dir.path(), "greeting.md", skill_content);

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        assert_eq!(skills.len(), 1);
        assert_eq!(skills[0].name, "greeting");
        assert_eq!(skills[0].description, "A simple greeting skill");
    }

    #[test]
    fn test_load_multiple_skill_files() {
        let temp_dir = setup_test_dir();

        let skill1 = r#"---
name: greeting
description: Greeting skill
---
Hello!
"#;
        let skill2 = r#"---
name: farewell
description: Farewell skill
---
Goodbye!
"#;

        create_skill_file(temp_dir.path(), "greeting.md", skill1);
        create_skill_file(temp_dir.path(), "farewell.md", skill2);

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        assert_eq!(skills.len(), 2);

        let names: Vec<&str> = skills.iter().map(|s| s.name.as_str()).collect();
        assert!(names.contains(&"greeting"));
        assert!(names.contains(&"farewell"));
    }

    #[test]
    fn test_load_skills_from_subdirectories() {
        let temp_dir = setup_test_dir();

        let skill1 = r#"---
name: greeting
description: Personal greeting
---
Hello!
"#;
        let skill2 = r#"---
name: meeting
description: Work meeting skill
---
Let's meet!
"#;

        create_skill_file(temp_dir.path(), "personal/greeting.md", skill1);
        create_skill_file(temp_dir.path(), "work/meeting.md", skill2);

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        assert_eq!(skills.len(), 2);

        // 验证命名空间
        let greeting = skills.iter().find(|s| s.name == "greeting").unwrap();
        assert_eq!(greeting.namespace, "personal");

        let meeting = skills.iter().find(|s| s.name == "meeting").unwrap();
        assert_eq!(meeting.namespace, "work");
    }

    #[test]
    fn test_extract_namespace_nested() {
        let temp_dir = setup_test_dir();

        let skill = r#"---
name: reply
description: Email reply
---
Reply to email
"#;

        create_skill_file(temp_dir.path(), "work/email/reply.md", skill);

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        assert_eq!(skills.len(), 1);
        assert_eq!(skills[0].namespace, "work:email");
    }

    #[test]
    fn test_skip_non_markdown_files() {
        let temp_dir = setup_test_dir();

        let skill_content = r#"---
name: greeting
description: Greeting skill
---
Hello!
"#;

        create_skill_file(temp_dir.path(), "greeting.md", skill_content);
        create_skill_file(temp_dir.path(), "README.txt", "This is a readme");
        create_skill_file(temp_dir.path(), "config.json", "{}");

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        // 只应该加载 .md 文件
        assert_eq!(skills.len(), 1);
        assert_eq!(skills[0].name, "greeting");
    }

    #[test]
    fn test_load_empty_directory() {
        let temp_dir = setup_test_dir();

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        assert_eq!(skills.len(), 0);
    }

    #[test]
    fn test_ignore_patterns_basic() {
        let temp_dir = setup_test_dir();

        let skill1 = r#"---
name: greeting
description: Normal skill
---
Hello!
"#;
        let skill2 = r#"---
name: draft
description: Draft skill
---
Draft content
"#;

        create_skill_file(temp_dir.path(), "greeting.md", skill1);
        create_skill_file(temp_dir.path(), "draft.md", skill2);

        // 创建 .ignore 文件
        let ignore_content = "draft.md\n";
        fs::write(temp_dir.path().join(".ignore"), ignore_content).unwrap();

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        // 只应该加载 greeting.md，draft.md 应该被忽略
        assert_eq!(skills.len(), 1);
        assert_eq!(skills[0].name, "greeting");
    }

    #[test]
    fn test_ignore_patterns_wildcard() {
        let temp_dir = setup_test_dir();

        let skill1 = r#"---
name: greeting
description: Normal skill
---
Hello!
"#;
        let skill2 = r#"---
name: temp1
description: Temp skill 1
---
Temp
"#;
        let skill3 = r#"---
name: temp2
description: Temp skill 2
---
Temp
"#;

        create_skill_file(temp_dir.path(), "greeting.md", skill1);
        create_skill_file(temp_dir.path(), "temp1.md", skill2);
        create_skill_file(temp_dir.path(), "temp2.md", skill3);

        // 创建 .ignore 文件，使用通配符
        let ignore_content = "temp*.md\n";
        fs::write(temp_dir.path().join(".ignore"), ignore_content).unwrap();

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        // 只应该加载 greeting.md
        assert_eq!(skills.len(), 1);
        assert_eq!(skills[0].name, "greeting");
    }

    #[test]
    fn test_ignore_patterns_directory() {
        let temp_dir = setup_test_dir();

        let skill1 = r#"---
name: greeting
description: Normal skill
---
Hello!
"#;
        let skill2 = r#"---
name: draft1
description: Draft skill 1
---
Draft
"#;
        let skill3 = r#"---
name: draft2
description: Draft skill 2
---
Draft
"#;

        create_skill_file(temp_dir.path(), "greeting.md", skill1);
        create_skill_file(temp_dir.path(), "drafts/draft1.md", skill2);
        create_skill_file(temp_dir.path(), "drafts/draft2.md", skill3);

        // 创建 .ignore 文件，忽略整个目录
        let ignore_content = "drafts/\n";
        fs::write(temp_dir.path().join(".ignore"), ignore_content).unwrap();

        let loader = SkillLoader::new(temp_dir.path().to_path_buf()).unwrap();
        let skills = loader.load_all().unwrap();

        // 只应该加载 greeting.md，drafts 目录下的文件应该被忽略
        assert_eq!(skills.len(), 1);
        assert_eq!(skills[0].name, "greeting");
    }
}
