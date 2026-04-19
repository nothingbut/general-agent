use uuid::Uuid;

#[derive(Debug, Clone, PartialEq)]
pub struct FileReference {
    pub raw: String,
    pub target: FileTarget,
}

#[derive(Debug, Clone, PartialEq)]
pub enum FileTarget {
    ById(Uuid),
    ByName(String),
}

pub fn parse_file_references(input: &str) -> Vec<FileReference> {
    let mut refs = Vec::new();
    let mut chars = input.char_indices().peekable();

    while let Some((i, ch)) = chars.next() {
        if ch != '@' {
            continue;
        }

        let rest = &input[i..];
        if !rest.starts_with("@file:") {
            continue;
        }

        let after_prefix = &input[i + 6..];
        if after_prefix.is_empty() {
            continue;
        }

        let (value, raw_len) = if after_prefix.starts_with('"') {
            match after_prefix[1..].find('"') {
                Some(end) => {
                    let val = &after_prefix[1..1 + end];
                    (val.to_string(), 6 + 1 + end + 1)
                }
                None => continue,
            }
        } else {
            let end = after_prefix
                .find(|c: char| c.is_whitespace() || c == ',' || c == ';' || c == ')' || c == ']')
                .unwrap_or(after_prefix.len());
            if end == 0 {
                continue;
            }
            (after_prefix[..end].to_string(), 6 + end)
        };

        let raw = input[i..i + raw_len].to_string();
        let target = match Uuid::parse_str(&value) {
            Ok(id) => FileTarget::ById(id),
            Err(_) => FileTarget::ByName(value),
        };

        refs.push(FileReference { raw, target });

        for _ in 0..raw_len.saturating_sub(1) {
            chars.next();
        }
    }

    refs
}

pub fn replace_file_references(input: &str, replacements: &[(String, String)]) -> String {
    let mut result = input.to_string();
    for (raw, replacement) in replacements {
        result = result.replace(raw, replacement);
    }
    result
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_by_name() {
        let refs = parse_file_references("请看 @file:readme.md 这个文件");
        assert_eq!(refs.len(), 1);
        assert_eq!(refs[0].raw, "@file:readme.md");
        assert_eq!(refs[0].target, FileTarget::ByName("readme.md".to_string()));
    }

    #[test]
    fn test_parse_by_uuid() {
        let id = Uuid::new_v4();
        let input = format!("请查看 @file:{}", id);
        let refs = parse_file_references(&input);
        assert_eq!(refs.len(), 1);
        assert_eq!(refs[0].target, FileTarget::ById(id));
    }

    #[test]
    fn test_parse_quoted_name() {
        let refs = parse_file_references("请看 @file:\"my document.md\" 这个");
        assert_eq!(refs.len(), 1);
        assert_eq!(refs[0].raw, "@file:\"my document.md\"");
        assert_eq!(
            refs[0].target,
            FileTarget::ByName("my document.md".to_string())
        );
    }

    #[test]
    fn test_parse_multiple_refs() {
        let refs = parse_file_references("比较 @file:a.rs 和 @file:b.rs");
        assert_eq!(refs.len(), 2);
        assert_eq!(refs[0].target, FileTarget::ByName("a.rs".to_string()));
        assert_eq!(refs[1].target, FileTarget::ByName("b.rs".to_string()));
    }

    #[test]
    fn test_parse_no_refs() {
        let refs = parse_file_references("没有文件引用");
        assert!(refs.is_empty());
    }

    #[test]
    fn test_parse_at_end_of_string() {
        let refs = parse_file_references("查看 @file:test.py");
        assert_eq!(refs.len(), 1);
        assert_eq!(refs[0].target, FileTarget::ByName("test.py".to_string()));
    }

    #[test]
    fn test_parse_with_comma_separator() {
        let refs = parse_file_references("文件 @file:a.rs,@file:b.rs");
        assert_eq!(refs.len(), 2);
        assert_eq!(refs[0].target, FileTarget::ByName("a.rs".to_string()));
        assert_eq!(refs[1].target, FileTarget::ByName("b.rs".to_string()));
    }

    #[test]
    fn test_parse_not_file_ref() {
        let refs = parse_file_references("@user:john @skill:greeting");
        assert!(refs.is_empty());
    }

    #[test]
    fn test_parse_empty_after_prefix() {
        let refs = parse_file_references("@file: 后面是空格");
        assert!(refs.is_empty());
    }

    #[test]
    fn test_replace_references() {
        let input = "看看 @file:readme.md 的内容";
        let replacements = vec![(
            "@file:readme.md".to_string(),
            "[文件: readme.md]\n# Hello World\n[/文件]".to_string(),
        )];

        let result = replace_file_references(input, &replacements);
        assert!(result.contains("[文件: readme.md]"));
        assert!(result.contains("# Hello World"));
        assert!(!result.contains("@file:"));
    }
}
