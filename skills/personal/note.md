---
name: note
description: Take a quick note with optional categorization
parameters:
  - name: content
    type: string
    required: true
    description: The note content to save
  - name: category
    type: string
    required: false
    description: Category or tag for the note (e.g., ideas, todos, references)
    default: general
---

# Note-Taking Skill

Save the following note:

**Content:** {content}
**Category:** {category}

Please confirm the note has been saved and provide a brief summary. If the note is particularly long, ask if the user wants to split it into multiple notes.

## Usage Tips

- Keep notes concise and actionable
- Use categories to organize related notes
- Review notes periodically to maintain relevance
