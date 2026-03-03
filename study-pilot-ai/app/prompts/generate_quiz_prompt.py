def get_generate_quiz_prompt(concept_names: list[str], question_count: int) -> str:
    concepts_str = ", ".join(concept_names[:8]) if concept_names else "material"
    return """{count} MCQ(s) for: {concepts}. Each: "text", "options" (4 strings), "correctAnswer". JSON array only.""".format(
        count=question_count,
        concepts=concepts_str,
    )
