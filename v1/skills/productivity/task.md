---
name: task
description: Create and track a task with deadlines and priorities
parameters:
  - name: title
    type: string
    required: true
    description: Task title or description
  - name: deadline
    type: string
    required: false
    description: When the task should be completed
  - name: priority
    type: string
    required: false
    description: Task priority (low, medium, high, urgent)
    default: medium
  - name: tags
    type: string
    required: false
    description: Comma-separated tags for categorization
---

# Task Management Skill

Create a new task with the following details:

**Title:** {title}
**Deadline:** {deadline}
**Priority:** {priority}
**Tags:** {tags}

Please:
1. Confirm the task has been created
2. Suggest next steps if applicable
3. Ask if the user wants to break down large tasks into subtasks

## Task Management Tips

- Break down complex tasks into smaller, actionable items
- Set realistic deadlines
- Review and prioritize tasks regularly
- Use tags for easy filtering and organization
