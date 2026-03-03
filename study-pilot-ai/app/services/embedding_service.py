import logging
import os

import httpx
from fastapi import HTTPException

from app.core.config import Settings

logger = logging.getLogger(__name__)

OPENROUTER_EMBEDDINGS_URL = "https://openrouter.ai/api/v1/embeddings"
OPENAI_EMBEDDINGS_URL = "https://api.openai.com/v1/embeddings"
EMBEDDING_DIMENSIONS = 1536


async def create_embeddings(texts: list[str], settings: Settings) -> list[list[float]]:
    """
    Embeddings: OpenRouter or OpenAI when keys set; otherwise Ollama (local) for Ollama-only setups.
    Returns list of 1536-dim vectors (padded/truncated when using Ollama's nomic-embed-text).
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

    return await _embed_via_ollama(texts, settings)


def _mock_embedding() -> list[float]:
    return [0.0] * EMBEDDING_DIMENSIONS


def _pad_or_truncate(emb: list[float], target: int = EMBEDDING_DIMENSIONS) -> list[float]:
    if len(emb) == target:
        return emb
    if len(emb) < target:
        return emb + [0.0] * (target - len(emb))
    return emb[:target]


async def _embed_via_ollama(texts: list[str], settings: Settings) -> list[list[float]]:
    """Use local Ollama embedding model (e.g. nomic-embed-text) when no OpenRouter/OpenAI key. Pads to 1536 dims."""
    logger.info("Using Ollama for embeddings (no OpenRouter/OpenAI key). Pull nomic-embed-text if needed: ollama pull nomic-embed-text")
    base = (settings.ollama_base_url or "").strip() or "http://localhost:11434"
    url = f"{base.rstrip('/')}/api/embed"
    model = getattr(settings, "ollama_embedding_model", None) or "nomic-embed-text"
    timeout = getattr(settings, "ollama_request_timeout", 120.0) or 60.0
    try:
        async with httpx.AsyncClient(timeout=timeout) as client:
            # Ollama accepts "input" as string or array of strings
            payload = {"model": model, "input": texts if len(texts) > 1 else (texts[0] if texts else "")}
            r = await client.post(url, json=payload)
            r.raise_for_status()
            data = r.json()
    except httpx.HTTPStatusError as e:
        if e.response.status_code >= 500:
            logger.warning("Ollama embeddings 5xx: %s", e.response.status_code)
            raise HTTPException(503, "Embedding service temporarily unavailable. Please retry.") from e
        raise
    except httpx.ConnectError as e:
        logger.warning("Ollama not reachable at %s: %s", base, e)
        raise HTTPException(503, "Ollama embedding service not reachable. Is Ollama running?") from e
    raw = data.get("embeddings") or []
    out: list[list[float]] = [_pad_or_truncate(emb if isinstance(emb, list) else []) for emb in raw]
    while len(out) < len(texts):
        out.append(_pad_or_truncate([]))
    return out[: len(texts)]


async def _embed_via_openrouter(texts: list[str], api_key: str, settings: Settings) -> list[list[float]]:
    model = settings.embedding_model or "openai/text-embedding-3-small"
    try:
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
    except httpx.HTTPStatusError as e:
        if e.response.status_code >= 500:
            logger.warning("OpenRouter embeddings upstream 5xx: %s", e.response.status_code)
            raise HTTPException(503, "Embedding service temporarily unavailable. Please retry.") from e
        raise
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
    try:
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
    except httpx.HTTPStatusError as e:
        if e.response.status_code >= 500:
            logger.warning("OpenAI embeddings upstream 5xx: %s", e.response.status_code)
            raise HTTPException(503, "Embedding service temporarily unavailable. Please retry.") from e
        raise
    items = data.get("data") or []
    out: list[list[float]] = []
    for item in items:
        emb = item.get("embedding") or []
        if len(emb) != EMBEDDING_DIMENSIONS:
            emb = (emb + [0.0] * EMBEDDING_DIMENSIONS)[:EMBEDDING_DIMENSIONS]
        out.append(emb)
    return out
