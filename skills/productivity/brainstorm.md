---
name: brainstorm
description: Facilitate a brainstorming session on a given topic
parameters:
  - name: topic
    type: string
    required: true
    description: The topic or problem to brainstorm about
  - name: perspective
    type: string
    required: false
    description: Perspective to approach from (creative, analytical, practical, critical)
    default: creative
---

# Brainstorming Skill

Let's brainstorm ideas about: **{topic}**

Approach: **{perspective}**

Please:
1. Generate 5-7 diverse ideas or solutions
2. For each idea, provide a brief explanation
3. Highlight the most promising ideas
4. Ask follow-up questions to deepen exploration

## Brainstorming Guidelines

- Encourage wild ideas - no judgment initially
- Build on others' ideas
- Stay focused on the topic
- Aim for quantity first, quality second
- Combine and refine ideas iteratively
