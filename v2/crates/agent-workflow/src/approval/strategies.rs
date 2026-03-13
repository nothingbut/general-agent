//! 审批策略辅助模块
//!
//! 提供条件评估等策略相关的功能。

use anyhow::{Result, bail};

/// 条件评估器
///
/// 评估字符串形式的条件表达式。
/// 简化版实现，支持基本的比较操作。
pub struct ConditionEvaluator;

impl ConditionEvaluator {
    /// 评估条件表达式
    ///
    /// # 支持的格式
    ///
    /// - `key < value` - 小于
    /// - `key > value` - 大于
    /// - `key == value` - 等于
    /// - `key != value` - 不等于
    /// - `key <= value` - 小于等于
    /// - `key >= value` - 大于等于
    ///
    /// # 示例
    ///
    /// ```rust,ignore
    /// let context = serde_json::json!({"cost": 50});
    /// let result = ConditionEvaluator::evaluate("cost < 100", &context)?;
    /// assert!(result);
    /// ```
    pub fn evaluate(condition: &str, context: &serde_json::Value) -> Result<bool> {
        let condition = condition.trim();

        // 解析条件表达式
        if let Some(pos) = condition.find("==") {
            let (key, value) = Self::split_at(condition, pos, 2);
            Self::compare_eq(key, value, context)
        } else if let Some(pos) = condition.find("!=") {
            let (key, value) = Self::split_at(condition, pos, 2);
            Self::compare_ne(key, value, context)
        } else if let Some(pos) = condition.find("<=") {
            let (key, value) = Self::split_at(condition, pos, 2);
            Self::compare_le(key, value, context)
        } else if let Some(pos) = condition.find(">=") {
            let (key, value) = Self::split_at(condition, pos, 2);
            Self::compare_ge(key, value, context)
        } else if let Some(pos) = condition.find('<') {
            let (key, value) = Self::split_at(condition, pos, 1);
            Self::compare_lt(key, value, context)
        } else if let Some(pos) = condition.find('>') {
            let (key, value) = Self::split_at(condition, pos, 1);
            Self::compare_gt(key, value, context)
        } else {
            bail!("Unsupported condition format: {}", condition)
        }
    }

    fn split_at(s: &str, pos: usize, op_len: usize) -> (&str, &str) {
        let key = s[..pos].trim();
        let value = s[pos + op_len..].trim();
        (key, value)
    }

    fn get_context_value<'a>(key: &str, context: &'a serde_json::Value) -> Option<&'a serde_json::Value> {
        context.get(key)
    }

    fn parse_number(s: &str) -> Option<f64> {
        s.trim_matches('"').trim_matches('\'').parse::<f64>().ok()
    }

    fn compare_eq(key: &str, value: &str, context: &serde_json::Value) -> Result<bool> {
        let ctx_value = Self::get_context_value(key, context)
            .ok_or_else(|| anyhow::anyhow!("Key not found in context: {}", key))?;

        // 尝试数值比较
        if let (Some(ctx_num), Some(val_num)) = (ctx_value.as_f64(), Self::parse_number(value)) {
            let diff: f64 = ctx_num - val_num;
            return Ok(diff.abs() < f64::EPSILON);
        }

        // 字符串比较
        if let Some(ctx_str) = ctx_value.as_str() {
            let val_str = value.trim_matches('"').trim_matches('\'');
            return Ok(ctx_str == val_str);
        }

        Ok(false)
    }

    fn compare_ne(key: &str, value: &str, context: &serde_json::Value) -> Result<bool> {
        Self::compare_eq(key, value, context).map(|r| !r)
    }

    fn compare_lt(key: &str, value: &str, context: &serde_json::Value) -> Result<bool> {
        let ctx_value = Self::get_context_value(key, context)
            .ok_or_else(|| anyhow::anyhow!("Key not found in context: {}", key))?;

        let ctx_num = ctx_value.as_f64()
            .ok_or_else(|| anyhow::anyhow!("Context value is not a number"))?;
        let val_num = Self::parse_number(value)
            .ok_or_else(|| anyhow::anyhow!("Comparison value is not a number"))?;

        Ok(ctx_num < val_num)
    }

    fn compare_gt(key: &str, value: &str, context: &serde_json::Value) -> Result<bool> {
        let ctx_value = Self::get_context_value(key, context)
            .ok_or_else(|| anyhow::anyhow!("Key not found in context: {}", key))?;

        let ctx_num = ctx_value.as_f64()
            .ok_or_else(|| anyhow::anyhow!("Context value is not a number"))?;
        let val_num = Self::parse_number(value)
            .ok_or_else(|| anyhow::anyhow!("Comparison value is not a number"))?;

        Ok(ctx_num > val_num)
    }

    fn compare_le(key: &str, value: &str, context: &serde_json::Value) -> Result<bool> {
        let ctx_value = Self::get_context_value(key, context)
            .ok_or_else(|| anyhow::anyhow!("Key not found in context: {}", key))?;

        let ctx_num = ctx_value.as_f64()
            .ok_or_else(|| anyhow::anyhow!("Context value is not a number"))?;
        let val_num = Self::parse_number(value)
            .ok_or_else(|| anyhow::anyhow!("Comparison value is not a number"))?;

        Ok(ctx_num <= val_num)
    }

    fn compare_ge(key: &str, value: &str, context: &serde_json::Value) -> Result<bool> {
        let ctx_value = Self::get_context_value(key, context)
            .ok_or_else(|| anyhow::anyhow!("Key not found in context: {}", key))?;

        let ctx_num = ctx_value.as_f64()
            .ok_or_else(|| anyhow::anyhow!("Context value is not a number"))?;
        let val_num = Self::parse_number(value)
            .ok_or_else(|| anyhow::anyhow!("Comparison value is not a number"))?;

        Ok(ctx_num >= val_num)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_evaluate_less_than() {
        let context = serde_json::json!({"cost": 50});
        assert!(ConditionEvaluator::evaluate("cost < 100", &context).unwrap());
        assert!(!ConditionEvaluator::evaluate("cost < 30", &context).unwrap());
    }

    #[test]
    fn test_evaluate_greater_than() {
        let context = serde_json::json!({"priority": 10});
        assert!(ConditionEvaluator::evaluate("priority > 5", &context).unwrap());
        assert!(!ConditionEvaluator::evaluate("priority > 20", &context).unwrap());
    }

    #[test]
    fn test_evaluate_equals() {
        let context = serde_json::json!({"status": "ready"});
        assert!(ConditionEvaluator::evaluate("status == \"ready\"", &context).unwrap());
        assert!(!ConditionEvaluator::evaluate("status == \"done\"", &context).unwrap());
    }

    #[test]
    fn test_evaluate_not_equals() {
        let context = serde_json::json!({"status": "pending"});
        assert!(ConditionEvaluator::evaluate("status != \"done\"", &context).unwrap());
        assert!(!ConditionEvaluator::evaluate("status != \"pending\"", &context).unwrap());
    }

    #[test]
    fn test_evaluate_less_equal() {
        let context = serde_json::json!({"count": 5});
        assert!(ConditionEvaluator::evaluate("count <= 5", &context).unwrap());
        assert!(ConditionEvaluator::evaluate("count <= 10", &context).unwrap());
        assert!(!ConditionEvaluator::evaluate("count <= 3", &context).unwrap());
    }

    #[test]
    fn test_evaluate_greater_equal() {
        let context = serde_json::json!({"age": 18});
        assert!(ConditionEvaluator::evaluate("age >= 18", &context).unwrap());
        assert!(ConditionEvaluator::evaluate("age >= 10", &context).unwrap());
        assert!(!ConditionEvaluator::evaluate("age >= 20", &context).unwrap());
    }

    #[test]
    fn test_evaluate_missing_key() {
        let context = serde_json::json!({});
        assert!(ConditionEvaluator::evaluate("cost < 100", &context).is_err());
    }
}
