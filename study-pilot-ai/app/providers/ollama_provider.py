import json
import httpx
from tenacity import retry, stop_after_attempt, wait_exponential

from app.core.config import Settings
from app.prompts import get_chat_prompt, get_extract_concepts_prompt, get_generate_quiz_prompt
from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_array, parse_json_object


class OllamaProvider(LLMProvider):
    """Local Ollama API provider. No API key; uses OLLAMA_BASE_URL and OLLAMA_MODEL (e.g. llama3:8b)."""

    def __init__(self, settings: Settings):
        base = (settings.ollama_base_url or "").strip() or "http://localhost:11434"
        self._base_url = base.rstrip("/")
        self._model = settings.ollama_model or "llama3:8b"
        self._timeout = settings.request_timeout
        self._stream = getattr(settings, "ollama_stream", True)

    @retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=1, max=10))
    async def _chat(self, messages: list[dict]) -> str:
        async with httpx.AsyncClient(timeout=self._timeout) as client:
            r = await client.post(
                f"{self._base_url}/api/chat",
                json={
                    "model": self._model,
                    "messages": messages,
                    "stream": False,
                    "options": {"temperature": 0.3},
                },
            )
            r.raise_for_status()
            data = r.json()
            msg = data.get("message") or {}
            return (msg.get("content") or "").strip()

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
        """Stream tokens from Ollama so the UI gets progressive updates and doesn't freeze."""
        if not self._stream:
            result = await self.chat(system, question, context, explanation_style)
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
