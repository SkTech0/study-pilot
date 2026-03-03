from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    ai_mode: str = "real"  # "real" | "mock" — mock skips external LLM calls for local testing
    openai_api_key: str = ""
    model_name: str = "gpt-4o-mini"
    gemini_api_key: str = ""
    gemini_model: str = "gemini-2.5-flash"
    deepseek_api_key: str = ""
    deepseek_model: str = "deepseek-chat"
    openrouter_api_key: str = ""
    openrouter_model: str = "google/gemini-2.5-flash"  # free alternative: google/gemini-2.0-flash-exp:free
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "llama3:8b"
    ollama_stream: bool = True  # stream tokens so UI doesn't freeze; set False to get full response in one chunk
    ollama_request_timeout: float = 120.0  # seconds; local models can be slow, use ≥120 for concept extraction
    llm_provider: str = "gemini"  # legacy: "gemini" or "openai"
    llm_fallback_chain: str = "gemini,deepseek,openrouter,openai"  # comma-separated; first is primary; 429/failure tries next
    request_timeout: float = 60.0
    embedding_model: str = "openai/text-embedding-3-small"  # for OpenRouter; use "text-embedding-3-small" for direct OpenAI
