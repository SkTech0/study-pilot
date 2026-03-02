import json
import logging
import os
import asyncio
import time
import httpx
from tenacity import retry, stop_after_attempt, retry_if_exception

from app.core.config import Settings
from app.prompts import get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider

logger = logging.getLogger(__name__)

def _is_429(e: BaseException) -> bool:
    return isinstance(e, httpx.HTTPStatusError) and e.response.status_code == 429


def _wait_429(retry_state):
    """Wait Retry-After seconds if present, else exponential backoff (15s, 30s, 60s, …)."""
    exc = retry_state.outcome.exception() if retry_state.outcome else None
    if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code == 429:
        ra = exc.response.headers.get("retry-after")
        if ra and str(ra).isdigit():
            return float(ra)
    # 15, 30, 60, 120, 120, …
    attempt = retry_state.attempt_number
    return min(15 * (2 ** min(attempt - 1, 3)), 120)


def _before_sleep_429(retry_state):
    """Log a clear message when backing off on 429."""
    wait = _wait_429(retry_state)
    attempt = retry_state.attempt_number
    logger.warning(
        "Gemini rate limit (429). Retrying in %.0fs (attempt %d/8). "
        "If this persists, wait a few minutes or use an API key with higher quota.",
        wait,
        attempt,
    )


def _strip_fences(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith("```"):
        lines = raw.split("\n")
        raw = "\n".join(lines[1:-1]) if lines[0].startswith("```json") else "\n".join(lines[1:-1])
    return raw.strip()


def _repair_truncated_json_array(raw: str) -> str | None:
    """If the LLM response was truncated (e.g. Unterminated string), try to close at last complete object."""
    if not raw.strip():
        return None
    s = raw.strip()
    if not s.startswith("["):
        s = "[" + s
    # Try cutting at each "}," (end of a complete object); use the rightmost that parses.
    for i in range(len(s) - 1, -1, -1):
        if i + 1 < len(s) and s[i] == "}" and s[i + 1] == ",":
            candidate = s[: i + 1] + "]"
            try:
                parsed = json.loads(candidate)
                if isinstance(parsed, list):
                    return candidate
            except json.JSONDecodeError:
                continue
    # Try cutting at any "}" (might be single object or last object without trailing comma)
    for i in range(len(s) - 1, -1, -1):
        if s[i] == "}":
            candidate = s[: i + 1]
            if not candidate.strip().endswith("]"):
                candidate += "]"
            try:
                parsed = json.loads(candidate)
                if isinstance(parsed, list):
                    return candidate
            except json.JSONDecodeError:
                continue
    return None


def _parse_json_array(raw: str) -> list[dict]:
    raw = _strip_fences(raw)
    try:
        out = json.loads(raw)
        return out if isinstance(out, list) else [out] if isinstance(out, dict) else []
    except json.JSONDecodeError as e:
        repaired = _repair_truncated_json_array(raw)
        if repaired:
            try:
                out = json.loads(repaired)
                if isinstance(out, list):
                    logger.warning("Quiz JSON was truncated; used %d complete question(s).", len(out))
                    return out
            except json.JSONDecodeError:
                pass
        logger.warning("Could not parse quiz JSON from LLM: %s. Returning empty list.", e)
        return []


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
        stop=stop_after_attempt(8),
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
        return _parse_json_array(content)

    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        names = [c.get("name", "") for c in concepts if isinstance(c, dict)]
        if not names:
            names = [str(c) for c in concepts]
        prompt = get_generate_quiz_prompt(names, count)
        content = await self._generate(prompt)
        return _parse_json_array(content)
