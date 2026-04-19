//! Markdown 渲染 — 聊天窗口代码块高亮

use ratatui::{
    style::{Modifier, Style},
    text::{Line, Span},
};

use super::colors::AppColors;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum BlockState {
    Normal,
    CodeBlock,
}

pub fn render_markdown(text: &str) -> Vec<Line<'_>> {
    let mut lines = Vec::new();
    let mut state = BlockState::Normal;
    for raw_line in text.lines() {
        match state {
            BlockState::Normal => {
                if let Some(rest) = raw_line.strip_prefix("```") {
                    state = BlockState::CodeBlock;
                    let lang = rest.trim();

                    let label = if lang.is_empty() {
                        "┌─ code ".to_string()
                    } else {
                        format!("┌─ {} ", lang)
                    };
                    lines.push(Line::from(Span::styled(
                        label,
                        Style::default()
                            .fg(AppColors::WARNING)
                            .add_modifier(Modifier::DIM),
                    )));
                } else {
                    lines.push(render_inline_markdown(raw_line));
                }
            }
            BlockState::CodeBlock => {
                if raw_line.starts_with("```") {
                    lines.push(Line::from(Span::styled(
                        "└─────",
                        Style::default()
                            .fg(AppColors::WARNING)
                            .add_modifier(Modifier::DIM),
                    )));
                    state = BlockState::Normal;
                } else {
                    lines.push(render_code_line(raw_line));
                }
            }
        }
    }

    if state == BlockState::CodeBlock {
        lines.push(Line::from(Span::styled(
            "└─────",
            Style::default()
                .fg(AppColors::WARNING)
                .add_modifier(Modifier::DIM),
        )));
    }

    lines
}

fn render_code_line(line: &str) -> Line<'_> {
    Line::from(vec![
        Span::styled(
            "│ ",
            Style::default()
                .fg(AppColors::WARNING)
                .add_modifier(Modifier::DIM),
        ),
        Span::styled(
            line,
            Style::default().fg(AppColors::FOCUS),
        ),
    ])
}

fn render_inline_markdown(line: &str) -> Line<'_> {
    if line.is_empty() {
        return Line::from("");
    }

    if let Some(rest) = line.strip_prefix("### ") {
        return Line::from(Span::styled(
            rest,
            Style::default()
                .fg(AppColors::INFO)
                .add_modifier(Modifier::BOLD),
        ));
    }
    if let Some(rest) = line.strip_prefix("## ") {
        return Line::from(Span::styled(
            rest,
            Style::default()
                .fg(AppColors::INFO)
                .add_modifier(Modifier::BOLD),
        ));
    }
    if let Some(rest) = line.strip_prefix("# ") {
        return Line::from(Span::styled(
            rest,
            Style::default()
                .fg(AppColors::INFO)
                .add_modifier(Modifier::BOLD | Modifier::UNDERLINED),
        ));
    }

    if let Some(rest) = line.strip_prefix("- ") {
        return Line::from(vec![
            Span::styled("• ", Style::default().fg(AppColors::FOCUS)),
            Span::raw(rest),
        ]);
    }
    if let Some(rest) = line.strip_prefix("* ") {
        return Line::from(vec![
            Span::styled("• ", Style::default().fg(AppColors::FOCUS)),
            Span::raw(rest),
        ]);
    }

    if let Some(rest) = line.strip_prefix("> ") {
        return Line::from(vec![
            Span::styled("▎ ", Style::default().fg(AppColors::NORMAL)),
            Span::styled(
                rest,
                Style::default()
                    .fg(AppColors::NORMAL)
                    .add_modifier(Modifier::ITALIC),
            ),
        ]);
    }

    render_inline_spans(line)
}

fn render_inline_spans(line: &str) -> Line<'_> {
    let mut spans = Vec::new();
    let mut chars = line.char_indices().peekable();
    let mut segment_start = 0;

    while let Some(&(i, ch)) = chars.peek() {
        if ch == '`' {
            if i > segment_start {
                spans.push(Span::raw(&line[segment_start..i]));
            }
            chars.next();
            let code_start = i + 1;
            let mut code_end = code_start;
            let mut found_close = false;
            while let Some(&(j, c2)) = chars.peek() {
                chars.next();
                if c2 == '`' {
                    code_end = j;
                    found_close = true;
                    break;
                }
                code_end = j + c2.len_utf8();
            }
            if found_close {
                spans.push(Span::styled(
                    &line[code_start..code_end],
                    Style::default()
                        .fg(AppColors::FOCUS)
                        .add_modifier(Modifier::BOLD),
                ));
            } else {
                spans.push(Span::raw(&line[i..code_end]));
            }
            segment_start = code_end + 1;
        } else if ch == '*' && chars.clone().nth(1).map(|(_, c)| c) == Some('*') {
            if i > segment_start {
                spans.push(Span::raw(&line[segment_start..i]));
            }
            chars.next();
            chars.next();
            let bold_start = i + 2;
            let mut bold_end = bold_start;
            let mut found_close = false;
            while let Some(&(j, c2)) = chars.peek() {
                if c2 == '*' && chars.clone().nth(1).map(|(_, c)| c) == Some('*') {
                    bold_end = j;
                    found_close = true;
                    chars.next();
                    chars.next();
                    break;
                }
                chars.next();
                bold_end = j + c2.len_utf8();
            }
            if found_close {
                spans.push(Span::styled(
                    &line[bold_start..bold_end],
                    Style::default().add_modifier(Modifier::BOLD),
                ));
                segment_start = bold_end + 2;
            } else {
                spans.push(Span::raw(&line[i..bold_end]));
                segment_start = bold_end;
            }
        } else {
            chars.next();
        }
    }

    if segment_start < line.len() {
        spans.push(Span::raw(&line[segment_start..]));
    }

    if spans.is_empty() {
        Line::from(Span::raw(line))
    } else {
        Line::from(spans)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_plain_text() {
        let lines = render_markdown("Hello world");
        assert_eq!(lines.len(), 1);
    }

    #[test]
    fn test_code_block() {
        let input = "before\n```rust\nfn main() {}\n```\nafter";
        let lines = render_markdown(input);
        assert_eq!(lines.len(), 5);
        assert!(format!("{:?}", lines[1]).contains("rust"));
    }

    #[test]
    fn test_unclosed_code_block() {
        let input = "```python\nprint('hello')";
        let lines = render_markdown(input);
        assert_eq!(lines.len(), 3);
    }

    #[test]
    fn test_heading() {
        let lines = render_markdown("# Title\n## Subtitle\n### Section");
        assert_eq!(lines.len(), 3);
    }

    #[test]
    fn test_bullet_list() {
        let lines = render_markdown("- item1\n* item2");
        assert_eq!(lines.len(), 2);
    }

    #[test]
    fn test_blockquote() {
        let lines = render_markdown("> quoted text");
        assert_eq!(lines.len(), 1);
    }

    #[test]
    fn test_inline_code() {
        let lines = render_markdown("use `tokio` for async");
        assert_eq!(lines.len(), 1);
        let line = &lines[0];
        assert!(line.spans.len() >= 3);
    }

    #[test]
    fn test_bold_text() {
        let lines = render_markdown("this is **bold** text");
        assert_eq!(lines.len(), 1);
        let line = &lines[0];
        assert!(line.spans.len() >= 3);
    }

    #[test]
    fn test_empty_text() {
        let lines = render_markdown("");
        assert!(lines.is_empty());
    }

    #[test]
    fn test_mixed_content() {
        let input = "# Hello\n\nSome `code` here\n\n```\nblock\n```\n\n- list item";
        let lines = render_markdown(input);
        assert!(lines.len() >= 7);
    }
}
