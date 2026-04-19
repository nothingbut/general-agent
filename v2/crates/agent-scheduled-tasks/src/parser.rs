use chrono::{DateTime, Utc};
use cron::Schedule;
use regex::Regex;
use std::str::FromStr;

use crate::error::{TaskError, Result};
use crate::models::ScheduleType;

pub struct ParsedSchedule {
    pub schedule_type: ScheduleType,
    pub cron_expression: String,
    pub description: String,
}

pub fn parse_schedule(input: &str) -> Result<ParsedSchedule> {
    if let Ok(parsed) = parse_cron(input) {
        return Ok(parsed);
    }
    parse_natural(input)
}

pub fn parse_cron(input: &str) -> Result<ParsedSchedule> {
    let cron_str = input.trim();

    let parts: Vec<&str> = cron_str.split_whitespace().collect();
    let full_cron = if parts.len() == 5 {
        format!("0 {}", cron_str)
    } else if parts.len() == 6 || parts.len() == 7 {
        cron_str.to_string()
    } else {
        return Err(TaskError::InvalidCron(format!("无效的 Cron 表达式: {}", input)));
    };

    Schedule::from_str(&full_cron)
        .map_err(|e| TaskError::InvalidCron(format!("{}: {}", input, e)))?;

    Ok(ParsedSchedule {
        schedule_type: ScheduleType::Cron,
        cron_expression: full_cron,
        description: format!("Cron: {}", cron_str),
    })
}

pub fn parse_natural(input: &str) -> Result<ParsedSchedule> {
    let input = input.trim();

    if let Some(cron) = try_every_day_at(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    if let Some(cron) = try_every_n_minutes(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    if let Some(cron) = try_every_n_hours(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    if let Some(cron) = try_weekday_at(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    if let Some(cron) = try_weekly_day_at(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    if let Some(cron) = try_monthly_at(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    if let Some(cron) = try_every_day_simple(input) {
        return Ok(ParsedSchedule {
            schedule_type: ScheduleType::Natural,
            cron_expression: cron,
            description: input.to_string(),
        });
    }

    Err(TaskError::InvalidSchedule(format!("无法解析调度表达式: {}", input)))
}

pub fn next_execution_time(cron_expression: &str) -> Result<DateTime<Utc>> {
    let schedule = Schedule::from_str(cron_expression)
        .map_err(|e| TaskError::InvalidCron(e.to_string()))?;

    schedule
        .upcoming(Utc)
        .next()
        .ok_or_else(|| TaskError::InvalidCron("无法计算下次执行时间".into()))
}

fn parse_time(time_str: &str) -> Option<(u32, u32)> {
    let re_pm = Regex::new(r"(?:下午|晚上|傍晚)\s*(\d{1,2})\s*[点时]?").ok()?;
    if let Some(caps) = re_pm.captures(time_str) {
        let h: u32 = caps[1].parse().ok()?;
        if h <= 12 {
            let hour = if h == 12 { 12 } else { h + 12 };
            return Some((hour, 0));
        }
    }

    let re_ampm = Regex::new(r"(?:上午|早上|早晨)\s*(\d{1,2})\s*[点时]?").ok()?;
    if let Some(caps) = re_ampm.captures(time_str) {
        let h: u32 = caps[1].parse().ok()?;
        if h <= 12 {
            return Some((h, 0));
        }
    }

    let re_hm = Regex::new(r"(\d{1,2})[:\s时](\d{1,2})").ok()?;
    if let Some(caps) = re_hm.captures(time_str) {
        let h: u32 = caps[1].parse().ok()?;
        let m: u32 = caps[2].parse().ok()?;
        if h < 24 && m < 60 {
            return Some((h, m));
        }
    }

    let re_h = Regex::new(r"(\d{1,2})\s*[点时]").ok()?;
    if let Some(caps) = re_h.captures(time_str) {
        let h: u32 = caps[1].parse().ok()?;
        if h < 24 {
            return Some((h, 0));
        }
    }

    None
}

fn try_every_day_at(input: &str) -> Option<String> {
    let re = Regex::new(r"每天\s*(.+)").ok()?;
    let caps = re.captures(input)?;
    let (h, m) = parse_time(&caps[1])?;
    Some(format!("0 {} {} * * *", m, h))
}

fn try_every_day_simple(input: &str) -> Option<String> {
    if input.contains("每天") || input.contains("每日") {
        return Some("0 0 9 * * *".to_string());
    }
    None
}

fn try_every_n_minutes(input: &str) -> Option<String> {
    let re = Regex::new(r"每\s*(\d+)\s*分钟").ok()?;
    let caps = re.captures(input)?;
    let n: u32 = caps[1].parse().ok()?;
    if n > 0 && n <= 59 {
        Some(format!("0 */{} * * * *", n))
    } else {
        None
    }
}

fn try_every_n_hours(input: &str) -> Option<String> {
    let re = Regex::new(r"每\s*(\d+)\s*(?:小时|个小时)").ok()?;
    let caps = re.captures(input)?;
    let n: u32 = caps[1].parse().ok()?;
    if n > 0 && n <= 23 {
        Some(format!("0 0 */{} * * *", n))
    } else {
        None
    }
}

fn try_weekday_at(input: &str) -> Option<String> {
    let re = Regex::new(r"工作日\s*(.+)").ok()?;
    let caps = re.captures(input)?;
    let (h, m) = parse_time(&caps[1])?;
    Some(format!("0 {} {} * * 1-5", m, h))
}

fn try_weekly_day_at(input: &str) -> Option<String> {
    let re = Regex::new(r"每(?:周|星期)\s*([一二三四五六日天])\s*(.*)").ok()?;
    let caps = re.captures(input)?;
    let day = match &caps[1] {
        "一" => "1",
        "二" => "2",
        "三" => "3",
        "四" => "4",
        "五" => "5",
        "六" => "6",
        "日" | "天" => "0",
        _ => return None,
    };

    let time_str = caps[2].trim();
    let (h, m) = if time_str.is_empty() {
        (9, 0)
    } else {
        parse_time(time_str)?
    };

    Some(format!("0 {} {} * * {}", m, h, day))
}

fn try_monthly_at(input: &str) -> Option<String> {
    let re = Regex::new(r"每月\s*(\d+)\s*[号日]\s*(.*)").ok()?;
    let caps = re.captures(input)?;
    let day: u32 = caps[1].parse().ok()?;
    if day < 1 || day > 31 {
        return None;
    }

    let time_str = caps[2].trim();
    let (h, m) = if time_str.is_empty() {
        (9, 0)
    } else {
        parse_time(time_str)?
    };

    Some(format!("0 {} {} {} * *", m, h, day))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_cron_5_parts() {
        let result = parse_cron("0 9 * * 1-5").unwrap();
        assert_eq!(result.schedule_type, ScheduleType::Cron);
        assert_eq!(result.cron_expression, "0 0 9 * * 1-5");
    }

    #[test]
    fn test_parse_cron_6_parts() {
        let result = parse_cron("0 30 9 * * *").unwrap();
        assert_eq!(result.cron_expression, "0 30 9 * * *");
    }

    #[test]
    fn test_parse_cron_invalid() {
        let result = parse_cron("invalid");
        assert!(result.is_err());
    }

    #[test]
    fn test_parse_natural_every_day() {
        let result = parse_natural("每天上午9点").unwrap();
        assert_eq!(result.schedule_type, ScheduleType::Natural);
        assert_eq!(result.cron_expression, "0 0 9 * * *");
    }

    #[test]
    fn test_parse_natural_every_day_time() {
        let result = parse_natural("每天下午3点").unwrap();
        assert_eq!(result.cron_expression, "0 0 15 * * *");
    }

    #[test]
    fn test_parse_natural_every_n_minutes() {
        let result = parse_natural("每30分钟").unwrap();
        assert_eq!(result.cron_expression, "0 */30 * * * *");
    }

    #[test]
    fn test_parse_natural_every_n_hours() {
        let result = parse_natural("每2小时").unwrap();
        assert_eq!(result.cron_expression, "0 0 */2 * * *");
    }

    #[test]
    fn test_parse_natural_weekday() {
        let result = parse_natural("工作日早上9点").unwrap();
        assert_eq!(result.cron_expression, "0 0 9 * * 1-5");
    }

    #[test]
    fn test_parse_natural_weekly() {
        let result = parse_natural("每周一下午3点").unwrap();
        assert_eq!(result.cron_expression, "0 0 15 * * 1");
    }

    #[test]
    fn test_parse_natural_monthly() {
        let result = parse_natural("每月1号上午10点").unwrap();
        assert_eq!(result.cron_expression, "0 0 10 1 * *");
    }

    #[test]
    fn test_parse_natural_simple_every_day() {
        let result = parse_natural("每天").unwrap();
        assert_eq!(result.cron_expression, "0 0 9 * * *");
    }

    #[test]
    fn test_parse_natural_invalid() {
        let result = parse_natural("随便什么时候");
        assert!(result.is_err());
    }

    #[test]
    fn test_parse_schedule_auto_detect() {
        let cron = parse_schedule("0 9 * * *").unwrap();
        assert_eq!(cron.schedule_type, ScheduleType::Cron);

        let natural = parse_schedule("每天上午9点").unwrap();
        assert_eq!(natural.schedule_type, ScheduleType::Natural);
    }

    #[test]
    fn test_next_execution_time() {
        let next = next_execution_time("0 0 9 * * *").unwrap();
        assert!(next > Utc::now());
    }

    #[test]
    fn test_parse_time_formats() {
        assert_eq!(parse_time("9:30"), Some((9, 30)));
        assert_eq!(parse_time("9点"), Some((9, 0)));
        assert_eq!(parse_time("上午9点"), Some((9, 0)));
        assert_eq!(parse_time("下午3点"), Some((15, 0)));
        assert_eq!(parse_time("晚上8点"), Some((20, 0)));
    }
}
