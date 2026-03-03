"""Build structured tutor prompt. Model must return JSON: message, nextStep, optionalExercise, citedChunkIds."""

from __future__ import annotations


def get_tutor_prompt(
    user_message: str,
    current_step: str,
    goals: list[dict],
    mastery_levels: list[dict],
    recent_mistakes: list[str],
    explanation_style: str | None,
    tone: str | None,
    context_chunks: list[dict],
) -> str:
    goals_str = "\n".join(
        f"- {g.get('conceptName', '')} ({g.get('goalType', '')}): {g.get('progressPercent', 0)}%"
        for g in goals
    ) or "(no goals)"
    mastery_str = "\n".join(
        f"- {m.get('conceptName', '')}: {m.get('masteryScore', 0)}%"
        for m in mastery_levels
    ) or "(none)"
    mistakes_str = ", ".join(recent_mistakes) if recent_mistakes else "None"
    ctx_lines = []
    for c in context_chunks or []:
        cid = c.get("chunkId") or c.get("chunk_id") or ""
        text = (c.get("text") or "").strip()
        if cid and text:
            ctx_lines.append(f"[chunkId={cid}] {text}")
    context_block = "\n".join(ctx_lines) if ctx_lines else "(no context)"

    step_instructions = {
        "Diagnose": "Ask a short probing question to understand what the learner knows. One question only.",
        "Explain": "Teach the concept clearly using the context. Do NOT skip steps. Be concise.",
        "Practice": "Provide one mini exercise (question + expected answer). Set optionalExercise.",
        "Evaluate": "Evaluate the learner's response. Confirm correct or explain the mistake.",
        "Reinforce": "Summarize the concept in 1-2 sentences (memory anchor).",
        "Complete": "Acknowledge completion and state the next goal or end the session.",
    }
    step_inst = step_instructions.get(current_step, "Respond as a tutor.")

    tone_inst = ""
    if tone:
        t = tone.lower()
        if t == "supportive":
            tone_inst = " Use a supportive, encouraging tone."
        elif t == "challenging":
            tone_inst = " Use a direct, challenging tone to push the learner."
        else:
            tone_inst = " Use a neutral, clear tone."

    style_inst = ""
    if explanation_style:
        s = explanation_style.lower()
        if s == "beginner":
            style_inst = " Use simple language and analogies."
        elif s == "advanced":
            style_inst = " Be concise and technical."

    return (
        "You are StudyPilot Tutor. Act ONLY as a tutor following the provided step. Do NOT skip steps.\n\n"
        f"CURRENT STEP: {current_step}\n"
        f"STEP INSTRUCTION: {step_inst}\n"
        f"TONE: {tone_inst}\n"
        f"STYLE: {style_inst}\n\n"
        "LEARNING GOALS:\n"
        f"{goals_str}\n\n"
        "MASTERY LEVELS:\n"
        f"{mastery_str}\n\n"
        "RECENT MISTAKES (concepts): "
        f"{mistakes_str}\n\n"
        "CONTEXT CHUNKS (use ONLY these; cite chunkIds):\n"
        f"{context_block}\n\n"
        "USER MESSAGE:\n"
        f"{user_message}\n\n"
        "Return ONLY valid JSON with exactly these keys:\n"
        '- "message": your tutor response (string)\n'
        '- "nextStep": one of Diagnose, Explain, Practice, Evaluate, Reinforce, Complete\n'
        '- "optionalExercise": only for Practice step: {"question": "...", "expectedAnswer": "...", "difficulty": "easy|medium|hard"}, else null\n'
        '- "citedChunkIds": array of chunkId strings you used (can be empty)\n'
        "No markdown, no extra text."
    )
