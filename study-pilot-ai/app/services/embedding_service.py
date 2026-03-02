import logging
import os

import httpx

from app.core.config import Settings

logger = logging.getLogger(__name__)

OPENROUTER_EMBEDDINGS_URL = "https://openrouter.ai/api/v1/embeddings"
OPENAI_EMBEDDINGS_URL = "https://api.openai.com/v1/embeddings"
EMBEDDING_DIMENSIONS = 1536


async def create_embeddings(texts: list[str], settings: Settings) -> list[list[float]]:
    """
    Embeddings override: use OpenRouter or OpenAI directly (not the LLM fallback chain).
    Returns list of 1536-dim vectors for text-embedding-3-small.
    """
    if not texts:
        return []

    if (settings.ai_mode or "real").strip().lower() == "mock":
        return [_mock_embedding() for _ in texts]

    openrouter_key = (settings.openrouter_api_key or os.environ.get("OPENROUTER_API_KEY", "")).strip()
    openai_key = (settings.openai_api_key or os.environ.get("OPENAI_API_KEY", "")).strip()

    if openrouter_key:
        return await _embed_via_openrouter(texts, openrouter_key, settings)
    if openai_key:
        return await _embed_via_openai(texts, openai_key, settings)

    logger.warning("No embedding API key (OpenRouter or OpenAI). Returning zero vectors.")
    return [[0.0] * EMBEDDING_DIMENSIONS for _ in texts]


def _mock_embedding() -> list[float]:
    return [0.0] * EMBEDDING_DIMENSIONS


async def _embed_via_openrouter(texts: list[str], api_key: str, settings: Settings) -> list[list[float]]:
    model = settings.embedding_model or "openai/text-embedding-3-small"
    async with httpx.AsyncClient(timeout=settings.request_timeout) as client:
        r = await client.post(
            OPENROUTER_EMBEDDINGS_URL,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            json={"model": model, "input": texts if len(texts) > 1 else texts[0]},
        )
        r.raise_for_status()
        data = r.json()
    items = data.get("data") or []
    if isinstance(items, dict):
        items = [items]
    out: list[list[float]] = []
    for item in items:
        emb = item.get("embedding") or []
        if len(emb) != EMBEDDING_DIMENSIONS:
            emb = (emb + [0.0] * EMBEDDING_DIMENSIONS)[:EMBEDDING_DIMENSIONS]
        out.append(emb)
    return out


async def _embed_via_openai(texts: list[str], api_key: str, settings: Settings) -> list[list[float]]:
    model = "text-embedding-3-small"
    if getattr(settings, "embedding_model", "").strip() and "openai/" not in (settings.embedding_model or ""):
        model = (settings.embedding_model or "text-embedding-3-small").strip()
    async with httpx.AsyncClient(timeout=settings.request_timeout) as client:
        r = await client.post(
            OPENAI_EMBEDDINGS_URL,
            headers={
                "Authorization": f"Bearer {api_key}",
                "Content-Type": "application/json",
            },
            json={"model": model, "input": texts},
        )
        r.raise_for_status()
        data = r.json()
    items = data.get("data") or []
    out: list[list[float]] = []
    for item in items:
        emb = item.get("embedding") or []
        if len(emb) != EMBEDDING_DIMENSIONS:
            emb = (emb + [0.0] * EMBEDDING_DIMENSIONS)[:EMBEDDING_DIMENSIONS]
        out.append(emb)
    return out
