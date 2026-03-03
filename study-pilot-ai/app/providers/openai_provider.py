import json
import os
import httpx
from tenacity import retry, stop_after_attempt, wait_exponential

from app.core.config import Settings
from app.prompts import get_chat_prompt, get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_array, parse_json_object


class OpenAIProvider(LLMProvider):
    def __init__(self, settings: Settings):
        self._api_key = settings.openai_api_key or os.environ.get("OPENAI_API_KEY", "")
        self._model = settings.model_name
        self._timeout = settings.request_timeout

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=1, max=10))
    async def _chat(self, messages: list[dict]) -> str:
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            r = await client.post(
                "https://api.openai.com/v1/chat/completions",
                headers={
                    "Authorization": f"Bearer {self._api_key}",
                    "Content-Type": "application/json",
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

    async def chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> dict:
        prompt = get_chat_prompt(system, question, context, explanation_style)
        content = await self._chat([{"role": "user", "content": prompt}])
        obj = parse_json_object(content)
        return {"answer": obj.get("answer", ""), "citedChunkIds": obj.get("citedChunkIds") or []}

    async def stream_chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ):
        prompt = get_chat_prompt(system, question, context, explanation_style)
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            r = await client.stream(
                "POST",
                "https://api.openai.com/v1/chat/completions",
                headers={
                    "Authorization": f"Bearer {self._api_key}",
                    "Content-Type": "application/json",
                },
                json={
                    "model": self._model,
                    "messages": [{"role": "user", "content": prompt}],
                    "temperature": 0.3,
                    "stream": True,
                },
            )
            r.raise_for_status()
            async for line in r.aiter_lines():
                if not line.strip() or line == "data: [DONE]":
                    continue
                if line.startswith("data: "):
                    try:
                        data = json.loads(line[6:])
                        for choice in data.get("choices", []):
                            delta = choice.get("delta", {})
                            content = delta.get("content")
                            if isinstance(content, str) and content:
                                yield content
                    except json.JSONDecodeError:
                        continue
