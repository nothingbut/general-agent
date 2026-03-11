//! JSON-RPC 2.0 协议实现

use serde::{Deserialize, Serialize};
use serde_json::Value;

/// JSON-RPC 消息类型
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(untagged)]
pub enum JsonRpcMessage {
    Request {
        jsonrpc: String,
        id: u64,
        method: String,
        params: Value,
    },
    Response {
        jsonrpc: String,
        id: u64,
        result: Value,
    },
    Error {
        jsonrpc: String,
        id: u64,
        error: JsonRpcError,
    },
}

/// JSON-RPC 错误对象
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct JsonRpcError {
    pub code: i32,
    pub message: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<Value>,
}

impl JsonRpcMessage {
    /// 创建请求消息
    pub fn request(id: u64, method: String, params: Value) -> Self {
        Self::Request {
            jsonrpc: "2.0".to_string(),
            id,
            method,
            params,
        }
    }

    /// 创建响应消息
    pub fn response(id: u64, result: Value) -> Self {
        Self::Response {
            jsonrpc: "2.0".to_string(),
            id,
            result,
        }
    }
}
