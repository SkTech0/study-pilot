"""Multi-LLM fallback adapter: tries providers in order until one succeeds."""
import logging
from typing import Sequence

from app.providers.base import LLMProvider

logger = logging.getLogger(__name__)


class FallbackAdapter(LLMProvider):
    """
    Enterprise multi-LLM adapter: runs the configured chain (e.g. Gemini → DeepSeek → OpenRouter).
    On each call, tries the first provider; on failure (exception or 429), tries the next.
    Logs which provider was used for observability.
    """

    def __init__(self, providers: Sequence[LLMProvider], names: Sequence[str] | None = None):
        if not providers:
            raise ValueError("FallbackAdapter requires at least one provider")
        self._providers = list(providers)
        self._names = list(names) if names else [f"provider_{i}" for i in range(len(self._providers))]

    async def extract_concepts(self, text: str) -> list[dict]:
        last_error: Exception | None = None
        for i, provider in enumerate(self._providers):
            name = self._names[i] if i < len(self._names) else f"provider_{i}"
            try:
                result = await provider.extract_concepts(text)
                logger.info("LLM extract_concepts succeeded with provider=%s", name)
                return result
            except Exception as e:
                last_error = e
                logger.warning(
                    "LLM extract_concepts failed with provider=%s: %s. Trying next.",
                    name,
                    e,
                    exc_info=False,
                )
        if last_error:
            raise last_error
        return []

    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        last_error: Exception | None = None
        for i, provider in enumerate(self._providers):
            name = self._names[i] if i < len(self._names) else f"provider_{i}"
            try:
                result = await provider.generate_questions(concepts, count)
                logger.info("LLM generate_questions succeeded with provider=%s", name)
                return result
            except Exception as e:
                last_error = e
                logger.warning(
                    "LLM generate_questions failed with provider=%s: %s. Trying next.",
                    name,
                    e,
                    exc_info=False,
                )
        if last_error:
            raise last_error
        return []
