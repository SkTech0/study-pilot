def get_extract_concepts_prompt(text: str) -> str:
    return """Extract key learning concepts from the following study content.
For each concept provide a short name and an optional brief description.

Content:
---
{text}
---

Respond with a JSON array of objects, each with "name" and "description" (description may be null).
Example: [{{"name": "Concept A", "description": "Brief explanation"}}, {{"name": "Concept B", "description": null}}]
Return only valid JSON, no markdown or extra text.""".format(
        text=text[:15000]
    )
