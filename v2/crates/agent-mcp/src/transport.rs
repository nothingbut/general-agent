//! MCP 传输层实现（基于 stdio）

use async_trait::async_trait;
use std::collections::HashMap;
use std::process::Stdio;
use std::sync::atomic::{AtomicU64, Ordering};
use tokio::io::{AsyncBufReadExt, AsyncWriteExt, BufReader};
use tokio::process::{Child, ChildStdin, ChildStdout, Command};
use crate::{JsonRpcMessage, Result, MCPError};

/// 传输层 trait
#[async_trait]
pub trait Transport: Send + Sync {
    /// 发送消息
    async fn send(&mut self, msg: JsonRpcMessage) -> Result<()>;

    /// 接收消息
    async fn recv(&mut self) -> Result<JsonRpcMessage>;
}

/// 基于 stdio 的传输层
pub struct StdioTransport {
    _process: Child,
    stdin: ChildStdin,
    stdout: BufReader<ChildStdout>,
    next_id: AtomicU64,
}

impl StdioTransport {
    /// 创建新的 stdio 传输层
    pub fn new(command: &str, args: &[String], env: &HashMap<String, String>) -> Result<Self> {
        let mut cmd = Command::new(command);
        cmd.args(args);
        cmd.envs(env);
        cmd.stdin(Stdio::piped());
        cmd.stdout(Stdio::piped());
        cmd.stderr(Stdio::inherit());

        let mut process = cmd.spawn()
            .map_err(|e| MCPError::ConnectionFailed(e.to_string()))?;

        let stdin = process.stdin.take()
            .ok_or_else(|| MCPError::ConnectionFailed("Failed to get stdin".to_string()))?;
        let stdout = process.stdout.take()
            .ok_or_else(|| MCPError::ConnectionFailed("Failed to get stdout".to_string()))?;

        Ok(Self {
            _process: process,
            stdin,
            stdout: BufReader::new(stdout),
            next_id: AtomicU64::new(1),
        })
    }

    /// 获取下一个消息 ID
    pub fn next_id(&self) -> u64 {
        self.next_id.fetch_add(1, Ordering::SeqCst)
    }
}

#[async_trait]
impl Transport for StdioTransport {
    async fn send(&mut self, msg: JsonRpcMessage) -> Result<()> {
        let json = serde_json::to_string(&msg)?;
        self.stdin.write_all(json.as_bytes()).await?;
        self.stdin.write_all(b"\n").await?;
        self.stdin.flush().await?;
        Ok(())
    }

    async fn recv(&mut self) -> Result<JsonRpcMessage> {
        let mut line = String::new();
        self.stdout.read_line(&mut line).await?;

        if line.is_empty() {
            return Err(MCPError::ConnectionFailed("EOF".to_string()));
        }

        let msg = serde_json::from_str(&line)?;
        Ok(msg)
    }
}
