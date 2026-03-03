import json
import logging

import httpx
from tenacity import retry, stop_after_attempt, wait_random_exponential

from app.core.config import Settings
from app.prompts import (
    get_chat_prompt,
    get_extract_concepts_prompt,
    get_generate_quiz_prompt,
)
from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_array, safe_parse_llm_json


logger = logging.getLogger(__name__)


class OllamaProvider(LLMProvider):
    """Local Ollama API provider. No API key; uses OLLAMA_BASE_URL and OLLAMA_MODEL (e.g. llama3:8b)."""

    supports_json_mode: bool = True

    def __init__(self, settings: Settings):
        base = (settings.ollama_base_url or "").strip() or "http://localhost:11434"
        self._base_url = base.rstrip("/")
        self._model = settings.ollama_model or "llama3:8b"
        # Use Ollama-specific timeout if set (local models often need >60s for long prompts)
        timeout = getattr(settings, "llm_timeout_seconds", None) or getattr(settings, "ollama_request_timeout", None)
        self._timeout = (
            float(timeout) if timeout is not None and float(timeout) > 0 else getattr(settings, "request_timeout", 30.0)
        )
        self._stream = getattr(settings, "ollama_stream", True)
        self._max_tokens = getattr(settings, "llm_max_tokens", 1024)
        logger.info(
            "Ollama provider configured: base_url=%s model=%s timeout=%.0fs stream=%s",
            self._base_url,
            self._model,
            self._timeout,
            self._stream,
        )

    @retry(stop=stop_after_attempt(3), wait=wait_random_exponential(multiplier=1, max=10))
    async def _chat(self, messages: list[dict], *, json_mode: bool = False) -> str:
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            payload: dict = {
                "model": self._model,
                "messages": messages,
                "stream": False,
                "options": {"temperature": 0.2, "num_predict": self._max_tokens},
            }
            if json_mode and self.supports_json_mode:
                payload["format"] = "json"

            r = await client.post(f"{self._base_url}/api/chat", json=payload)
            r.raise_for_status()
            data = r.json()
            msg = data.get("message") or {}
            return (msg.get("content") or "").strip()

    async def extract_concepts(self, text: str) -> list[dict]:
        prompt = get_extract_concepts_prompt(text)
        content = await self._chat([{"role": "user", "content": prompt}], json_mode=False)
        return parse_json_array(content)

    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        names = [c.get("name", "") for c in concepts if isinstance(c, dict)]
        if not names:
            names = [str(c) for c in concepts]
        prompt = get_generate_quiz_prompt(names, count)
        content = await self._chat([{"role": "user", "content": prompt}], json_mode=False)
        return parse_json_array(content)

    async def chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
        require_json: bool = True,
    ) -> dict:
        prompt = get_chat_prompt(system, question, context, explanation_style)

        json_system_message = (
            "You MUST respond ONLY with valid JSON.\n"
            "Do not include explanations.\n"
            "Do not include markdown.\n"
            "Output strictly:\n"
            "{\n"
            '  \"answer\": \"string\",\n'
            '  \"citedChunkIds\": [\"string\"]\n'
            "}\n"
        )

        messages: list[dict] = []
        if require_json:
            messages.append({"role": "system", "content": json_system_message})
        messages.append({"role": "user", "content": prompt})

        content = await self._chat(messages, json_mode=require_json)
        return safe_parse_llm_json(content)

    async def stream_chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ):
        """Stream tokens from Ollama so the UI gets progressive updates and doesn't freeze."""
        if not self._stream:
            result = await self.chat(system, question, context, explanation_style, require_json=False)
            answer = result.get("answer") or ""
            if answer:
                yield answer
            return
        prompt = get_chat_prompt(system, question, context, explanation_style)
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            async with client.stream(
                "POST",
                f"{self._base_url}/api/chat",
                json={
                    "model": self._model,
                    "messages": [{"role": "user", "content": prompt}],
                    "stream": True,
                    "options": {"temperature": 0.3},
                },
            ) as r:
                r.raise_for_status()
                async for line in r.aiter_lines():
                    if not line or not line.strip():
                        continue
                    try:
                        data = json.loads(line)
                        msg = data.get("message") or {}
                        content = msg.get("content")
                        if isinstance(content, str) and content:
                            yield content
                    except json.JSONDecodeError:
                        continue
