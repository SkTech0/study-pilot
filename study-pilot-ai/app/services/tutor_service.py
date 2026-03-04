import json
import logging
from typing import Any

from app.prompts.tutor_prompt import get_tutor_prompt
from app.providers.parse_utils import parse_json_object

logger = logging.getLogger(__name__)


async def tutor_respond(
    user_message: str,
    current_step: str,
    goals: list[dict],
    mastery_levels: list[dict],
    recent_mistakes: list[str],
    explanation_style: str | None,
    tone: str | None,
    retrieved_chunks: list[dict],
    provider: Any,
) -> dict[str, Any]:
    """
    Call LLM with tutor prompt. Returns dict with message, nextStep, optionalExercise, citedChunkIds.
    """
    prompt = get_tutor_prompt(
        user_message=user_message,
        current_step=current_step,
        goals=goals,
        mastery_levels=mastery_levels,
        recent_mistakes=recent_mistakes,
        explanation_style=explanation_style,
        tone=tone,
        context_chunks=retrieved_chunks,
    )
    result = await provider.chat(
        system=prompt,
        question=user_message,
        context=[{"chunkId": c.get("chunkId"), "documentId": c.get("documentId"), "text": c.get("text")} for c in retrieved_chunks],
        explanation_style=None,
    )
    raw = result.get("answer") or ""
    obj = parse_json_object(raw) if raw.strip() else {}
    message = str(obj.get("message") or "")
    next_step = str(obj.get("nextStep") or current_step)
    optional_exercise = obj.get("optionalExercise")
    if not isinstance(optional_exercise, dict):
        optional_exercise = None
    cited = obj.get("citedChunkIds") or obj.get("cited_chunk_ids") or []
    if not isinstance(cited, list):
        cited = []
    cited = [str(x) for x in cited]

    # Fallback: if model returned plain text in "answer" (e.g. wrong schema), show it so UI is not blank
    if not message and raw.strip():
        logger.warning(
            "Tutor response had no parseable message/nextStep; using raw answer as message (len=%d). "
            "Provider may have returned wrong JSON shape.",
            len(raw),
        )
        message = raw.strip()

    return {
        "message": message,
        "nextStep": next_step,
        "optionalExercise": optional_exercise,
        "citedChunkIds": cited,
    }


async def evaluate_exercise(
    question: str,
    expected_answer: str,
    user_answer: str,
    provider: Any,
) -> dict[str, Any]:
    """
    Call LLM to evaluate user answer. Returns dict with isCorrect, explanation.
    """
    prompt = (
        "You are a tutor evaluating a learner's answer.\n\n"
        f"QUESTION: {question}\n"
        f"EXPECTED ANSWER: {expected_answer}\n"
        f"LEARNER'S ANSWER: {user_answer}\n\n"
        "Return ONLY valid JSON with exactly these keys:\n"
        '- "isCorrect": true or false (true if the answer is substantially correct)\n'
        '- "explanation": brief explanation (1-2 sentences)\n'
        "No markdown, no extra text."
    )
    result = await provider.chat(
        system=prompt,
        question=user_answer,
        context=[],
        explanation_style=None,
    )
    raw = result.get("answer") or ""
    obj = parse_json_object(raw) if raw.strip() else {}
    return {
        "isCorrect": bool(obj.get("isCorrect", False)),
        "explanation": str(obj.get("explanation") or ""),
    }
