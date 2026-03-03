def get_generate_quiz_prompt(concept_names: list[str], question_count: int) -> str:
    concepts_str = ", ".join(concept_names) if concept_names else "the provided material"
    return """Generate {count} multiple-choice quiz question(s) based on these concepts: {concepts}.

Rules:
- Each question has exactly 4 options (A, B, C, D).
- Use "text" for the question, "options" for the 4 option strings, "correctAnswer" for the correct option text or the letter A/B/C/D.
- Keep question and options short (one sentence each).

Output ONLY a valid JSON array. No markdown, no code fence, no explanation before or after.
Example format: [{{"text":"What is X?","options":["A","B","C","D"],"correctAnswer":"A"}}]

Respond with only the JSON array:""".format(
        count=question_count,
        concepts=concepts_str,
    )
