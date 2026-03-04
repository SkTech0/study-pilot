from typing import Any

from app.models.schemas import ConceptIn, QuizQuestionOut


def _normalize_correct_answer(correct: str, options: list[str]) -> str:
    """Map LLM output (e.g. 'A', '1', or option text) to the exact option text for grading. Never guess—return raw if no match."""
    if not options:
        return (correct or "").strip()
    raw = (correct or "").strip()
    if not raw:
        return ""
    # Letter A–D (0-based index)
    if len(raw) == 1 and raw.upper() in "ABCD":
        idx = ord(raw.upper()) - ord("A")
        if 0 <= idx < len(options):
            return options[idx]
    # Digit 1–4 or 0–3
    if raw.isdigit():
        i = int(raw)
        if 1 <= i <= len(options):
            return options[i - 1]
        if 0 <= i < len(options):
            return options[i]
    # Already the option text (or close): find best match
    for opt in options:
        if opt.strip() == raw or (opt.strip() and raw and opt.strip().lower() == raw.lower()):
            return opt
    return raw


class QuizService:
    def __init__(self, router: Any):
        self._router = router

    async def generate_questions(
        self, concepts: list[ConceptIn], question_count: int
    ) -> list[QuizQuestionOut]:
        concept_dicts = [{"name": c.name} for c in concepts]
        raw = await self._router.execute("quiz", {"concepts": concept_dicts, "count": question_count})
        result: list[QuizQuestionOut] = []
        for item in raw if isinstance(raw, list) else []:
            if not isinstance(item, dict):
                continue
            options = item.get("options") or []
            if not isinstance(options, list):
                options = [str(o) for o in options] if options else []
            correct = item.get("correctAnswer") or item.get("correct_answer") or ""
            text = item.get("text") or item.get("question") or ""
            opts = [str(o) for o in (options[:4] if isinstance(options, list) else [])]
            if not text or not opts:
                continue
            correct_text = _normalize_correct_answer(str(correct).strip() if correct else "", opts)
            if not correct_text.strip() and str(correct).strip() in opts:
                correct_text = str(correct).strip()
            if not correct_text.strip():
                continue
            result.append(
                QuizQuestionOut(
                    text=str(text),
                    options=opts,
                    correct_answer=correct_text,
                )
            )
        return result
