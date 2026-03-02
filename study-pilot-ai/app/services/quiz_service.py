from app.models.schemas import ConceptIn, QuizQuestionOut
from app.providers.base import LLMProvider


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
            result.append(
                QuizQuestionOut(
                    text=str(text),
                    options=opts,
                    correct_answer=str(correct).strip() if correct else opts[0],
                )
            )
        return result
