from abc import ABC, abstractmethod
from typing import AsyncIterator


class LLMProvider(ABC):
    @abstractmethod
    async def extract_concepts(self, text: str) -> list[dict]:
        pass

    @abstractmethod
    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        pass

    @abstractmethod
    async def chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> dict:
        pass

    async def stream_chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> AsyncIterator[str]:
        """Stream tokens. Default: run chat once and yield full answer. Override for real streaming."""
        result = await self.chat(system, question, context, explanation_style)
        answer = result.get("answer") or ""
        if answer:
            yield answer
