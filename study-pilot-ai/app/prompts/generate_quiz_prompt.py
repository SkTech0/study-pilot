def get_generate_quiz_prompt(concept_names: list[str], question_count: int) -> str:
    concepts_str = ", ".join(concept_names) if concept_names else "the provided material"
    return """Generate {count} multiple-choice quiz questions based on these concepts: {concepts}.

Rules:
- Each question must have exactly 4 options.
- Include "correctAnswer" with the exact text of the correct option.
- Questions should test understanding, not just recall.

Respond with a JSON array of objects, each with:
- "text": the question text
- "options": array of 4 option strings
- "correctAnswer": the exact correct option text

Return only valid JSON, no markdown or extra text.""".format(
        count=question_count,
        concepts=concepts_str,
    )
