use agent_rag::loader::*;
use agent_core::traits::Document;
use std::fs;
use tempfile::TempDir;

#[tokio::test]
async fn test_markdown_loader() {
    let temp_dir = TempDir::new().unwrap();
    let file_path = temp_dir.path().join("test.md");
    fs::write(&file_path, "# Test\nContent").unwrap();

    let loader = MarkdownLoader;
    let docs = loader.load(file_path.to_str().unwrap()).await.unwrap();

    assert_eq!(docs.len(), 1);
    assert!(docs[0].content.contains("Test"));
}
