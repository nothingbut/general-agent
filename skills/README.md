# Skills Directory

This directory contains skill definitions for the General Agent system.

## Directory Structure

```
skills/
├── .ignore                    # Patterns for files to ignore
├── README.md                  # This file (ignored by .ignore)
├── personal/                  # Personal productivity skills
│   ├── reminder.md           # Create reminders
│   ├── note.md               # Take quick notes
│   └── greeting.md           # Greet users
└── productivity/              # Work and task management skills
    ├── task.md               # Manage tasks
    └── brainstorm.md         # Brainstorming sessions
```

## Skill File Format

Each skill is defined in a Markdown file with YAML frontmatter:

```markdown
---
name: skill-name
description: Brief description of what the skill does
parameters:
  - name: param_name
    type: string
    required: true
    description: What this parameter is for
    default: optional_default_value
---

# Skill Instructions

Your prompt/instructions for the LLM go here.

You can use {param_name} placeholders that will be replaced
with actual parameter values when the skill is invoked.
```

## Using Skills

Skills can be invoked using two syntaxes:

### 1. Short Syntax (`@skill` or `/skill`)
```
@greeting
@reminder task='Buy milk' time='5pm'
/note content='Meeting notes' category='work'
```

### 2. Full Syntax (namespace:skill)
```
@personal:reminder task='Call dentist' time='tomorrow'
/productivity:task title='Finish report' deadline='Friday'
```

## Namespaces

Skills are automatically organized by their directory structure:
- `personal/reminder.md` → `personal:reminder`
- `productivity/task.md` → `productivity:task`

If multiple skills have the same name in different namespaces, you must use the full syntax to disambiguate.

## Parameter Syntax

When invoking skills with parameters, use key='value' or key="value" format:
- Single quotes: `task='Buy groceries'`
- Double quotes: `task="Call mom"`
- Multiple parameters: `task='Meeting' time='2pm' priority='high'`

## Creating New Skills

1. Choose the appropriate namespace directory (or create a new one)
2. Create a `.md` file with YAML frontmatter
3. Define parameters (if needed)
4. Write clear instructions for the LLM
5. Test with `@skill-name` invocation

## .ignore Patterns

The `.ignore` file supports gitignore-style patterns:
- `**/draft-*.md` - Ignore files starting with "draft-"
- `**/*-draft.md` - Ignore files ending with "-draft"
- `**/README.md` - Ignore README files
- `**/.*` - Ignore hidden files

## Examples

### Simple Greeting (No Parameters)
```
@greeting
```

### Reminder with Parameters
```
@reminder task='Buy groceries' time='5pm' priority='high'
```

### Note Taking
```
@note content='Great idea for new feature' category='ideas'
```

### Task Management
```
@task title='Review PR' deadline='tomorrow' priority='high' tags='code-review,urgent'
```

### Brainstorming
```
@brainstorm topic='Marketing campaign ideas' perspective='creative'
```
