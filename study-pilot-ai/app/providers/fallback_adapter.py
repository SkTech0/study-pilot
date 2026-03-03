"""Multi-LLM fallback adapter: tries providers in order until one succeeds."""
import asyncio
import logging
from typing import AsyncIterator, Sequence

import httpx
from tenacity import RetryError

from app.providers.base import LLMProvider

logger = logging.getLogger(__name__)

# When a provider returns 429, wait before trying the next (avoids hammering next provider).
FALLBACK_DELAY_AFTER_429_SEC = 2.0


def _is_rate_limit(exc: Exception) -> bool:
    if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code == 429:
        return True
    if isinstance(exc, RetryError) and exc.last_attempt.failed():
        inner = exc.last_attempt.exception()
        return inner is not None and _is_rate_limit(inner)
    return False


def _unwrap_error(e: Exception) -> Exception:
    """Unwrap tenacity RetryError so we raise the underlying cause (e.g. HTTPStatusError)."""
    if isinstance(e, RetryError) and e.last_attempt.failed():
        exc = e.last_attempt.exception()
        if exc is not None:
            return exc
    return e


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
                last_error = _unwrap_error(e)
                logger.warning(
                    "LLM extract_concepts failed with provider=%s: %s. Failing over to next provider.",
                    name,
                    last_error,
                    exc_info=False,
                )
                if _is_rate_limit(e) and i + 1 < len(self._providers):
                    await asyncio.sleep(FALLBACK_DELAY_AFTER_429_SEC)
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
                last_error = _unwrap_error(e)
                logger.warning(
                    "LLM generate_questions failed with provider=%s: %s. Failing over to next provider.",
                    name,
                    last_error,
                    exc_info=False,
                )
                if _is_rate_limit(e) and i + 1 < len(self._providers):
                    await asyncio.sleep(FALLBACK_DELAY_AFTER_429_SEC)
        if last_error:
            raise last_error
        raise RuntimeError("No LLM providers available for generate_questions")

    async def chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> dict:
        last_error: Exception | None = None
        for i, provider in enumerate(self._providers):
            name = self._names[i] if i < len(self._names) else f"provider_{i}"
            try:
                result = await provider.chat(system, question, context, explanation_style)
                logger.info("LLM chat succeeded with provider=%s", name)
                return result if isinstance(result, dict) else {}
            except Exception as e:
                last_error = _unwrap_error(e)
                logger.warning(
                    "LLM chat failed with provider=%s: %s. Failing over to next provider.",
                    name,
                    last_error,
                    exc_info=False,
                )
                if _is_rate_limit(e) and i + 1 < len(self._providers):
                    await asyncio.sleep(FALLBACK_DELAY_AFTER_429_SEC)
        if last_error:
            raise last_error
        raise RuntimeError("No LLM providers available for chat")

    async def stream_chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> AsyncIterator[str]:
        last_error: Exception | None = None
        for i, provider in enumerate(self._providers):
            name = self._names[i] if i < len(self._names) else f"provider_{i}"
            try:
                async for token in provider.stream_chat(
                    system, question, context, explanation_style
                ):
                    yield token
                logger.info("LLM stream_chat succeeded with provider=%s", name)
                return
            except Exception as e:
                last_error = _unwrap_error(e)
                logger.warning(
                    "LLM stream_chat failed with provider=%s: %s. Failing over to next provider.",
                    name,
                    last_error,
                    exc_info=False,
                )
                if _is_rate_limit(e) and i + 1 < len(self._providers):
                    await asyncio.sleep(FALLBACK_DELAY_AFTER_429_SEC)
        if last_error:
            raise last_error
        raise RuntimeError("No LLM providers available for stream_chat")
