def get_generate_quiz_prompt(concept_names: list[str], question_count: int) -> str:
    concepts_str = ", ".join(concept_names) if concept_names else "the provided material"
    return """Generate {count} multiple-choice quiz questions based on these concepts: {concepts}.

Rules:
- Each question must have exactly 4 options.
- You MUST include "correctAnswer" for every question: use the exact text of the correct option (must match one of the option strings exactly), or the letter A/B/C/D for the correct option.
- Questions should test understanding, not just recall.
- Keep question and option text concise (one short sentence each).

Respond with a JSON array of objects, each with:
- "text": the question text
- "options": array of 4 option strings (order A, B, C, D)
- "correctAnswer": required—exact text of the correct option or the letter (A, B, C, or D)

Return only valid JSON, no markdown or extra text.""".format(
        count=question_count,
        concepts=concepts_str,
    )
