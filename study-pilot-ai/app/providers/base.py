from abc import ABC, abstractmethod
from typing import AsyncIterator


class LLMProvider(ABC):
    """Abstract base for all LLM providers.

    Subclasses should honour the chat() contract:
      - Never raise for normal inputs.
      - Always return {"answer": str, "citedChunkIds": list[str]}.
    """

    #: Whether this provider supports a native JSON/structured-output mode.
    supports_json_mode: bool = False

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
        require_json: bool = True,
    ) -> dict:
        """Synchronous-style chat call (non-streaming).

        Implementations must always return a dict with keys:
          - answer: str
          - citedChunkIds: list[str]
        and must not propagate provider-specific JSON parsing failures.
        """
        raise NotImplementedError

    async def stream_chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> AsyncIterator[str]:
        """Stream tokens. Default: call chat(require_json=False) and yield full answer once.

        Providers that support true token streaming should override this.
        """
        result = await self.chat(
            system, question, context, explanation_style, require_json=False
        )
        answer = result.get("answer") or ""
        if answer:
            yield answer
