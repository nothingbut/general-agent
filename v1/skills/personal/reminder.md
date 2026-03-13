---
name: reminder
description: Create a reminder for a specific task at a specific time
parameters:
  - name: task
    type: string
    required: true
    description: The task or activity to be reminded about
  - name: time
    type: string
    required: true
    description: When to send the reminder (e.g., "3pm", "tomorrow at 9am", "in 2 hours")
  - name: priority
    type: string
    required: false
    description: Priority level (low, normal, high)
    default: normal
---

# Reminder Skill

Create a reminder for the following task:

**Task:** {task}
**Time:** {time}
**Priority:** {priority}

Please confirm the reminder has been set and provide a brief acknowledgment. If the time format seems ambiguous, ask for clarification.

## Examples

- "Buy groceries" at "5pm today"
- "Call dentist" at "tomorrow morning"
- "Submit report" at "Friday 2pm" with "high" priority
