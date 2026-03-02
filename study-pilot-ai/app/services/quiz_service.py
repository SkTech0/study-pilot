from app.models.schemas import ConceptIn, QuizQuestionOut
from app.providers.base import LLMProvider


def _normalize_correct_answer(correct: str, options: list[str]) -> str:
    """Map LLM output (e.g. 'A', '1', or option text) to the exact option text for grading."""
    if not options:
        return (correct or "").strip()
    raw = (correct or "").strip()
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
    return raw or options[0]


class QuizService:
    def __init__(self, provider: LLMProvider):
        self._provider = provider

    async def generate_questions(
        self, concepts: list[ConceptIn], question_count: int
    ) -> list[QuizQuestionOut]:
        concept_dicts = [{"name": c.name} for c in concepts]
        raw = await self._provider.generate_questions(concept_dicts, question_count)
        result: list[QuizQuestionOut] = []
        for item in raw if isinstance(raw, list) else []:
            if not isinstance(item, dict):
                continue
            options = item.get("options") or []
            if not isinstance(options, list):
                options = [str(o) for o in options] if options else []
            correct = (
                item.get("correctAnswer")
                or item.get("correct_answer")
                or (options[0] if options else "")
            )
            text = item.get("text") or item.get("question") or ""
            opts = [str(o) for o in (options[:4] if isinstance(options, list) else [])]
            if not text or not opts:
                continue
            correct_text = _normalize_correct_answer(str(correct).strip() if correct else "", opts)
            result.append(
                QuizQuestionOut(
                    text=str(text),
                    options=opts,
                    correct_answer=correct_text,
                )
            )
        return result
