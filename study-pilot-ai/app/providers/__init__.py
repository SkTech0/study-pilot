import os

from app.providers.base import LLMProvider
from app.providers.gemini_provider import GeminiProvider
from app.providers.openai_provider import OpenAIProvider


def get_provider(settings):
    """Return the configured LLM provider (Gemini if key set, else OpenAI)."""
    use_gemini = (settings.llm_provider or "").strip().lower() == "gemini"
    has_gemini = bool((settings.gemini_api_key or "").strip() or os.environ.get("GEMINI_API_KEY"))
    has_openai = bool((settings.openai_api_key or "").strip() or os.environ.get("OPENAI_API_KEY"))
    if use_gemini and has_gemini:
        return GeminiProvider(settings)
    if has_openai:
        return OpenAIProvider(settings)
    if has_gemini:
        return GeminiProvider(settings)
    return OpenAIProvider(settings)


__all__ = ["LLMProvider", "OpenAIProvider", "GeminiProvider", "get_provider"]
