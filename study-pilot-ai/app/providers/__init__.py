import os
from typing import Any

from app.core.config import Settings
from app.providers.base import LLMProvider
from app.providers.deepseek_provider import DeepSeekProvider
from app.providers.fallback_adapter import FallbackAdapter
from app.providers.gemini_provider import GeminiProvider
from app.providers.openai_provider import OpenAIProvider
from app.providers.openrouter_provider import OpenRouterProvider


def _has_gemini_key(settings: Settings) -> bool:
    return bool(
        (settings.gemini_api_key or "").strip()
        or os.environ.get("GEMINI_API_KEY")
        or os.environ.get("GOOGLE_API_KEY")
    )


def _has_deepseek_key(settings: Settings) -> bool:
    return bool((settings.deepseek_api_key or "").strip() or os.environ.get("DEEPSEEK_API_KEY"))


def _has_openrouter_key(settings: Settings) -> bool:
    return bool((settings.openrouter_api_key or "").strip() or os.environ.get("OPENROUTER_API_KEY"))


def _has_openai_key(settings: Settings) -> bool:
    return bool((settings.openai_api_key or "").strip() or os.environ.get("OPENAI_API_KEY"))


def _build_fallback_chain(settings: Settings) -> list[tuple[str, LLMProvider]]:
    """Build ordered list of (name, provider) from LLM_FALLBACK_CHAIN, only including providers with keys."""
    chain_str = (settings.llm_fallback_chain or "").strip() or "gemini,deepseek,openrouter"
    names = [n.strip().lower() for n in chain_str.split(",") if n.strip()]
    out: list[tuple[str, LLMProvider]] = []
    for name in names:
        if name == "gemini" and _has_gemini_key(settings):
            out.append(("gemini", GeminiProvider(settings)))
        elif name == "deepseek" and _has_deepseek_key(settings):
            out.append(("deepseek", DeepSeekProvider(settings)))
        elif name == "openrouter" and _has_openrouter_key(settings):
            out.append(("openrouter", OpenRouterProvider(settings)))
        elif name == "openai" and _has_openai_key(settings):
            out.append(("openai", OpenAIProvider(settings)))
    return out


def get_provider(settings: Settings) -> LLMProvider:
    """
    Multi-LLM adapter: returns a FallbackAdapter over the configured chain (e.g. Gemini → DeepSeek → OpenRouter).
    Only providers with API keys set are included. If no provider in the chain has a key, falls back to legacy
    single-provider selection (LLM_PROVIDER + OpenAI/Gemini).
    """
    chain = _build_fallback_chain(settings)
    if chain:
        providers = [p for _, p in chain]
        names = [n for n, _ in chain]
        if len(providers) == 1:
            return providers[0]
        return FallbackAdapter(providers, names)

    # Legacy: no keys in fallback chain, use LLM_PROVIDER + gemini/openai
    use_gemini = (settings.llm_provider or "").strip().lower() == "gemini"
    if use_gemini and _has_gemini_key(settings):
        return GeminiProvider(settings)
    if _has_openai_key(settings):
        return OpenAIProvider(settings)
    if _has_gemini_key(settings):
        return GeminiProvider(settings)
    return OpenAIProvider(settings)


__all__ = [
    "LLMProvider",
    "OpenAIProvider",
    "GeminiProvider",
    "DeepSeekProvider",
    "OpenRouterProvider",
    "FallbackAdapter",
    "get_provider",
]
