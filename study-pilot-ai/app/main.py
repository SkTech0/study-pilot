from contextlib import asynccontextmanager
import logging
from logging.handlers import RotatingFileHandler
from pathlib import Path
import os

from dotenv import load_dotenv
from fastapi import FastAPI

from app.api.routes import router
from app.core.config import Settings
from app.core.exceptions import global_exception_handler
from app.services import ConceptService, QuizService
from app.providers import get_provider, get_provider_chain_names

load_dotenv()


def _configure_logging() -> logging.Logger:
    """
    Configure application logging to also write to a rotating file.
    Uses AI_LOG_LEVEL env var (default INFO).
    """
    log_level_name = os.getenv("AI_LOG_LEVEL", "INFO").upper()
    level = getattr(logging, log_level_name, logging.INFO)

    root_logger = logging.getLogger()
    root_logger.setLevel(level)

    # Create logs directory next to this file's parent (project root of AI service)
    base_dir = Path(__file__).resolve().parent.parent
    logs_dir = base_dir / "logs"
    logs_dir.mkdir(parents=True, exist_ok=True)
    log_path = logs_dir / "study-pilot-ai.log"

    file_handler = RotatingFileHandler(log_path, maxBytes=1_000_000, backupCount=5, encoding="utf-8")
    formatter = logging.Formatter(
        "%(asctime)s [%(levelname)s] %(name)s %(message)s"
    )
    file_handler.setFormatter(formatter)

    # Avoid adding duplicate handlers if this is reloaded (e.g. uvicorn --reload)
    if not any(isinstance(h, RotatingFileHandler) and getattr(h, "baseFilename", "") == str(log_path) for h in root_logger.handlers):
        root_logger.addHandler(file_handler)

    return logging.getLogger(__name__)


logger = _configure_logging()

_settings: Settings | None = None
_concept_service: ConceptService | None = None
_quiz_service: QuizService | None = None


def get_settings() -> Settings:
    global _settings
    if _settings is None:
        _settings = Settings()
    return _settings


def _get_provider():
    return get_provider(get_settings())


def get_concept_service() -> ConceptService:
    global _concept_service
    if _concept_service is None:
        _concept_service = ConceptService(_get_provider())
    return _concept_service


def get_quiz_service() -> QuizService:
    global _quiz_service
    if _quiz_service is None:
        _quiz_service = QuizService(_get_provider())
    return _quiz_service


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    chain = get_provider_chain_names(settings)
    logger.info("LLM provider chain (adapter): %s", ", ".join(chain) if chain else "none")
    provider = get_provider(settings)
    app.state.concept_service = ConceptService(provider)
    app.state.quiz_service = QuizService(provider)
    app.state.llm_provider = provider
    yield


app = FastAPI(lifespan=lifespan)
app.add_exception_handler(Exception, global_exception_handler)
app.include_router(router, prefix="", tags=["ai"])


@app.get("/health")
async def health():
    return {"status": "ok"}
