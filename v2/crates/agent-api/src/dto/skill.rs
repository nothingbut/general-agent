use serde::{Deserialize, Serialize};
use utoipa::ToSchema;

#[derive(Serialize, ToSchema)]
pub struct SkillDto {
    pub name: String,
    pub full_name: String,
    pub namespace: String,
    pub description: String,
    pub parameters: Vec<SkillParameterDto>,
}

#[derive(Serialize, ToSchema)]
pub struct SkillParameterDto {
    pub name: String,
    pub param_type: String,
    pub required: bool,
    pub description: String,
    pub default: Option<String>,
}

impl From<&agent_skills::SkillDefinition> for SkillDto {
    fn from(s: &agent_skills::SkillDefinition) -> Self {
        Self {
            name: s.name.clone(),
            full_name: s.full_name(),
            namespace: s.namespace.clone(),
            description: s.description.clone(),
            parameters: s.parameters.iter().map(SkillParameterDto::from).collect(),
        }
    }
}

impl From<&agent_skills::SkillParameter> for SkillParameterDto {
    fn from(p: &agent_skills::SkillParameter) -> Self {
        Self {
            name: p.name.clone(),
            param_type: p.param_type.clone(),
            required: p.required,
            description: p.description.clone(),
            default: p.default.clone(),
        }
    }
}

#[derive(Deserialize, ToSchema)]
pub struct InvokeSkillRequest {
    pub parameters: std::collections::HashMap<String, String>,
}
