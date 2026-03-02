import logging
from typing import Any

from app.providers.base import LLMProvider

logger = logging.getLogger(__name__)


async def chat(system: str, question: str, context: list[dict], provider: LLMProvider) -> dict[str, Any]:
    """
    RAG chat using the configured LLM fallback chain (gemini → deepseek → openrouter → openai).
    Returns {"answer": str, "citedChunkIds": list[str], "model": str|None}.
    """
    result = await provider.chat(system, question, context)
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
