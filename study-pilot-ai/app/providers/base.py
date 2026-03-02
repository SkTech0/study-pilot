from abc import ABC, abstractmethod


class LLMProvider(ABC):
    @abstractmethod
    async def extract_concepts(self, text: str) -> list[dict]:
        pass

    @abstractmethod
    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        pass

    @abstractmethod
    async def chat(self, system: str, question: str, context: list[dict]) -> dict:
        pass
