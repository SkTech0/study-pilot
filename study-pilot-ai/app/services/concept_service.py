from typing import Any

from app.models.schemas import ConceptOut


class ConceptService:
    def __init__(self, router: Any):
        self._router = router

    async def extract_concepts(self, text: str) -> list[ConceptOut]:
        raw = await self._router.execute("extract", {"text": text})
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
