use agent_mcp::protocol::*;
use serde_json::json;

#[test]
fn test_request_serialization() {
    let req = JsonRpcMessage::Request {
        jsonrpc: "2.0".to_string(),
        id: 1,
        method: "initialize".to_string(),
        params: json!({"protocolVersion": "2024-11-05"}),
    };

    let serialized = serde_json::to_string(&req).unwrap();
    assert!(serialized.contains("\"method\":\"initialize\""));
}

#[test]
fn test_response_deserialization() {
    let json_str = r#"{"jsonrpc":"2.0","id":1,"result":{"success":true}}"#;
    let msg: JsonRpcMessage = serde_json::from_str(json_str).unwrap();

    match msg {
        JsonRpcMessage::Response { id, result, .. } => {
            assert_eq!(id, 1);
            assert!(result.is_object());
        }
        _ => panic!("Expected Response"),
    }
}
