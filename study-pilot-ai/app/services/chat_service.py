import logging
from typing import Any, AsyncIterator

from app.providers.base import LLMProvider
from app.providers.parse_utils import parse_json_object

logger = logging.getLogger(__name__)

EMPTY_ANSWER_FALLBACK = "I'm temporarily unable to get a response from the AI. Please try again shortly."


async def stream_chat(
    system: str,
    question: str,
    context: list[dict],
    provider: LLMProvider | None = None,
    explanation_style: str | None = None,
    stream_source: AsyncIterator[str] | None = None,
) -> AsyncIterator[dict[str, Any]]:
    yield {"event": "start"}
    buffer: list[str] = []
    try:
        if stream_source is not None:
            async for token in stream_source:
                buffer.append(token)
                yield {"token": token}
        elif provider is not None:
            async for token in provider.stream_chat(system, question, context, explanation_style):
                buffer.append(token)
                yield {"token": token}
    except Exception as exc:
        logger.warning("stream_chat provider error: %s", exc)
        buffer.append(EMPTY_ANSWER_FALLBACK)
        yield {"token": EMPTY_ANSWER_FALLBACK}
    finally:
        if not buffer:
            yield {"token": EMPTY_ANSWER_FALLBACK}
            buffer.append(EMPTY_ANSWER_FALLBACK)
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
    RAG chat using the configured LLM fallback chain.
    Returns {"answer": str, "citedChunkIds": list[str], "model": str|None}.
    Never returns empty answer; uses fallback message if provider returns empty.
    """
    result = await provider.chat(system, question, context, explanation_style)
    if not isinstance(result, dict):
        return {"answer": EMPTY_ANSWER_FALLBACK, "citedChunkIds": [], "model": None}
    cited = result.get("citedChunkIds") or result.get("cited_chunk_ids") or []
    if not isinstance(cited, list):
        cited = []
    cited = [str(x) for x in cited]
    answer = str(result.get("answer") or "").strip()
    if not answer:
        logger.warning("LLM returned empty answer; using fallback")
        answer = EMPTY_ANSWER_FALLBACK
    return {
        "answer": answer,
        "citedChunkIds": cited,
        "model": result.get("model"),
    }
