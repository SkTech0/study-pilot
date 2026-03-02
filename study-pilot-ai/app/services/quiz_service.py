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
            if isinstance(item, dict):
                options = item.get("options") or []
                if not isinstance(options, list):
                    options = []
                result.append(
                    QuizQuestionOut(
                        text=item.get("text", ""),
                        options=options,
                        correct_answer=item.get("correctAnswer", ""),
                    )
                )
        return result
