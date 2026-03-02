from app.models.schemas import ConceptOut
from app.providers.base import LLMProvider


class ConceptService:
    def __init__(self, provider: LLMProvider):
        self._provider = provider

    async def extract_concepts(self, text: str) -> list[ConceptOut]:
        raw = await self._provider.extract_concepts(text)
        result: list[ConceptOut] = []
        for item in raw if isinstance(raw, list) else []:
            if isinstance(item, dict):
                result.append(
                    ConceptOut(
                        name=item.get("name", ""),
                        description=item.get("description"),
                    )
                )
        return result
