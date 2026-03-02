"""OpenRouter API provider (multi-model gateway, OpenAI-compatible)."""
import json
import logging
import os

import httpx
from tenacity import retry, stop_after_attempt, wait_exponential

from app.core.config import Settings
from app.prompts import get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_array

logger = logging.getLogger(__name__)

BASE_URL = "https://openrouter.ai/api/v1"


class OpenRouterProvider(LLMProvider):
    """OpenRouter gateway: one API key, many models. Uses OPENROUTER_API_KEY and OPENROUTER_MODEL."""

    def __init__(self, settings: Settings):
        self._api_key = (
            (settings.openrouter_api_key or os.environ.get("OPENROUTER_API_KEY", "")).strip()
        )
        self._model = settings.openrouter_model or "google/gemini-2.0-flash-exp:free"
        self._timeout = settings.request_timeout

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=1, max=10))
    async def _chat(self, messages: list[dict]) -> str:
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            r = await client.post(
                f"{BASE_URL}/chat/completions",
                headers={
                    "Authorization": f"Bearer {self._api_key}",
                    "Content-Type": "application/json",
                    "HTTP-Referer": "https://github.com/study-pilot",
                },
                json={
                    "model": self._model,
                    "messages": messages,
                    "temperature": 0.3,
                },
            )
            r.raise_for_status()
            data = r.json()
            return data["choices"][0]["message"]["content"]

    async def extract_concepts(self, text: str) -> list[dict]:
        prompt = get_extract_concepts_prompt(text)
        content = await self._chat([{"role": "user", "content": prompt}])
        return parse_json_array(content)

    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        names = [c.get("name", "") for c in concepts if isinstance(c, dict)]
        if not names:
            names = [str(c) for c in concepts]
        prompt = get_generate_quiz_prompt(names, count)
        content = await self._chat([{"role": "user", "content": prompt}])
        return parse_json_array(content)
