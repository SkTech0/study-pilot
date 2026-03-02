from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    openai_api_key: str = ""
    model_name: str = "gpt-4o-mini"
    gemini_api_key: str = ""
    gemini_model: str = "gemini-2.5-flash"
    deepseek_api_key: str = ""
    deepseek_model: str = "deepseek-chat"
    openrouter_api_key: str = ""
    openrouter_model: str = "google/gemini-2.0-flash-exp:free"
    llm_provider: str = "gemini"  # legacy: "gemini" or "openai"
    llm_fallback_chain: str = "gemini,deepseek,openrouter"  # comma-separated; first is primary
    request_timeout: float = 60.0
