"""DeepSeek API provider (OpenAI-compatible chat completions)."""
import json
import logging
import os

import httpx
from tenacity import retry, retry_if_exception, stop_after_attempt, wait_exponential

from app.core.config import Settings
from app.prompts import get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_array

logger = logging.getLogger(__name__)

BASE_URL = "https://api.deepseek.com"


def _retryable(e: BaseException) -> bool:
    """Retry only on 5xx or 429; fail fast on 4xx so fallback adapter can try next provider."""
    if not isinstance(e, httpx.HTTPStatusError):
        return False
    code = e.response.status_code
    return code == 429 or code >= 500


class DeepSeekProvider(LLMProvider):
    """DeepSeek API via OpenAI-compatible endpoint. Uses DEEPSEEK_API_KEY or settings.deepseek_api_key."""

    def __init__(self, settings: Settings):
        self._api_key = (settings.deepseek_api_key or os.environ.get("DEEPSEEK_API_KEY", "")).strip()
        self._model = settings.deepseek_model or "deepseek-chat"
        self._timeout = settings.request_timeout

    @retry(
        retry=retry_if_exception(_retryable),
        stop=stop_after_attempt(2),
        wait=wait_exponential(multiplier=1, min=1, max=5),
    )
    async def _chat(self, messages: list[dict]) -> str:
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            r = await client.post(
                f"{BASE_URL}/v1/chat/completions",
                headers={
                    "Authorization": f"Bearer {self._api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self._model,
                    "messages": messages,
                    "temperature": 0.3,
                    "response_format": {"type": "json_object"},
                },
            )
            r.raise_for_status()
            data = r.json()
            content = data["choices"][0]["message"]["content"]
            if isinstance(content, (dict, list)):
                content = json.dumps(content)
            return content

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
