import logging
from typing import Any, AsyncIterator

from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_object

logger = logging.getLogger(__name__)


async def stream_chat(
    system: str,
    question: str,
    context: list[dict],
    provider: LLMProvider,
    explanation_style: str | None = None,
) -> AsyncIterator[dict[str, Any]]:
    """
    Stream RAG chat tokens. Yields {"token": "..."} then {"done": True, "citedChunkIds": [...], "model": ...}.
    Buffers output to parse citedChunkIds from final JSON.
    """
    buffer: list[str] = []
    try:
        async for token in provider.stream_chat(
            system, question, context, explanation_style
        ):
            buffer.append(token)
            yield {"token": token}
    finally:
        full = "".join(buffer)
        obj = parse_json_object(full) if full.strip() else {}
        cited = obj.get("citedChunkIds") or obj.get("cited_chunk_ids") or []
        if not isinstance(cited, list):
            cited = []
        cited = [str(x) for x in cited]
        yield {"done": True, "citedChunkIds": cited, "model": None}


async def chat(
    system: str,
    question: str,
    context: list[dict],
    provider: LLMProvider,
    explanation_style: str | None = None,
) -> dict[str, Any]:
    """
    RAG chat using the configured LLM fallback chain (gemini → deepseek → openrouter → openai).
    Returns {"answer": str, "citedChunkIds": list[str], "model": str|None}.
    """
    result = await provider.chat(system, question, context, explanation_style)
    if not isinstance(result, dict):
        return {"answer": "", "citedChunkIds": [], "model": None}
    cited = result.get("citedChunkIds") or result.get("cited_chunk_ids") or []
    if not isinstance(cited, list):
        cited = []
    cited = [str(x) for x in cited]
    return {
        "answer": str(result.get("answer") or ""),
        "citedChunkIds": cited,
        "model": result.get("model"),
    }
