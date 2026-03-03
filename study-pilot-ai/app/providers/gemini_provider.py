import logging
import os
import asyncio
import time
import httpx
from tenacity import retry, stop_after_attempt, retry_if_exception

from app.core.config import Settings
from app.prompts import get_chat_prompt, get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_array, parse_json_object

logger = logging.getLogger(__name__)

def _is_429(e: BaseException) -> bool:
    return isinstance(e, httpx.HTTPStatusError) and e.response.status_code == 429


# Short wait on 429 so we can fail over to next provider quickly (user doesn't notice).
_GEMINI_429_WAIT_SEC = 2.0
_GEMINI_429_MAX_ATTEMPTS = 2  # One quick retry, then reraise so FallbackAdapter tries next model.


def _wait_429(retry_state):
    """Brief wait on 429 so fallback chain can try next provider without long delay."""
    exc = retry_state.outcome.exception() if retry_state.outcome else None
    if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code == 429:
        ra = exc.response.headers.get("retry-after")
        if ra and str(ra).isdigit():
            return min(float(ra), _GEMINI_429_WAIT_SEC)  # Cap so we don't block fallback
    return _GEMINI_429_WAIT_SEC


def _before_sleep_429(retry_state):
    """Log and then reraise after brief retry so adapter can switch to next provider."""
    attempt = retry_state.attempt_number
    logger.warning(
        "Gemini rate limit (429). Retrying once in %.0fs, then failing over to next provider.",
        _GEMINI_429_WAIT_SEC,
    )


# Minimum seconds between any two Gemini API calls to reduce 429s on free tier.
_GEMINI_CALL_INTERVAL_SEC = 5.0


class GeminiProvider(LLMProvider):
    """Google Gemini API provider. Uses GEMINI_API_KEY or settings.gemini_api_key."""

    BASE_URL = "https://generativelanguage.googleapis.com/v1beta"
    _semaphore = asyncio.Semaphore(1)
    _last_call_time: float = 0

    def __init__(self, settings: Settings):
        self._api_key = (
            (settings.gemini_api_key or os.environ.get("GEMINI_API_KEY", "")).strip()
            or os.environ.get("GOOGLE_API_KEY", "").strip()
        )
        self._model = settings.gemini_model or "gemini-2.5-flash"
        self._timeout = settings.request_timeout

    @retry(
        retry=retry_if_exception(_is_429),
        stop=stop_after_attempt(_GEMINI_429_MAX_ATTEMPTS),
        wait=_wait_429,
        before_sleep=_before_sleep_429,
        reraise=True,
    )
    async def _generate(self, prompt: str) -> str:
        url = f"{self.BASE_URL}/models/{self._model}:generateContent"
        async with self._semaphore:
            # Space out calls to reduce 429s on free tier
            now = time.monotonic()
            since_last = now - GeminiProvider._last_call_time
            if since_last < _GEMINI_CALL_INTERVAL_SEC:
                await asyncio.sleep(_GEMINI_CALL_INTERVAL_SEC - since_last)
            GeminiProvider._last_call_time = time.monotonic()

            async with httpx.AsyncClient(timeout=self._timeout) as client:
                r = await client.post(
                    url,
                    headers={
                        "x-goog-api-key": self._api_key,
                        "Content-Type": "application/json",
                    },
                    json={
                        "contents": [{"parts": [{"text": prompt}]}],
                        "generationConfig": {
                            "temperature": 0.3,
                            "maxOutputTokens": 2048,
                            "responseMimeType": "application/json",
                        },
                    },
                )
                r.raise_for_status()
                data = r.json()
        candidates = data.get("candidates") or []
        if not candidates:
            prompt_feedback = data.get("promptFeedback", {})
            raise ValueError(
                prompt_feedback.get("blockReason", "No candidates returned")
                or "Gemini returned no response"
            )
        parts = (candidates[0].get("content") or {}).get("parts") or []
        if not parts:
            raise ValueError("Gemini response had no text parts")
        return (parts[0].get("text") or "").strip()

    async def extract_concepts(self, text: str) -> list[dict]:
        prompt = get_extract_concepts_prompt(text)
        content = await self._generate(prompt)
        return parse_json_array(content)

    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        names = [c.get("name", "") for c in concepts if isinstance(c, dict)]
        if not names:
            names = [str(c) for c in concepts]
        prompt = get_generate_quiz_prompt(names, count)
        content = await self._generate(prompt)
        return parse_json_array(content)

    async def chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> dict:
        prompt = get_chat_prompt(system, question, context, explanation_style)
        content = await self._generate(prompt)
        obj = parse_json_object(content)
        return {"answer": obj.get("answer", ""), "citedChunkIds": obj.get("citedChunkIds") or []}
