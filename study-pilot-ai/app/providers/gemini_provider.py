import json
import os
import asyncio
import httpx
from tenacity import retry, stop_after_attempt, wait_exponential

from app.core.config import Settings
from app.prompts import get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider


def _parse_json_array(raw: str) -> list[dict]:
    raw = raw.strip()
    if raw.startswith("```"):
        lines = raw.split("\n")
        raw = "\n".join(lines[1:-1]) if lines[0].startswith("```json") else "\n".join(lines[1:-1])
    return json.loads(raw)


class GeminiProvider(LLMProvider):
    """Google Gemini API provider. Uses GEMINI_API_KEY or settings.gemini_api_key."""

    BASE_URL = "https://generativelanguage.googleapis.com/v1beta"
    _semaphore = asyncio.Semaphore(1)

    def __init__(self, settings: Settings):
        self._api_key = (
            (settings.gemini_api_key or os.environ.get("GEMINI_API_KEY", "")).strip()
            or os.environ.get("GOOGLE_API_KEY", "").strip()
        )
        self._model = settings.gemini_model or "gemini-2.5-flash"
        self._timeout = settings.request_timeout

    @retry(
        retry=lambda e: isinstance(e, httpx.HTTPStatusError) and e.response.status_code == 429,
        stop=stop_after_attempt(6),
        wait=wait_exponential(multiplier=2, min=10, max=120),
        reraise=True,
    )
    async def _generate(self, prompt: str) -> str:
        url = f"{self.BASE_URL}/models/{self._model}:generateContent"
        async with self._semaphore:
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
