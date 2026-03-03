def get_extract_concepts_prompt(text: str) -> str:
    return """Extract concepts. For each: "name", "description" (or null). Output JSON array only, no markdown.

{text}""".format(
        text=text[:12000]
    )
