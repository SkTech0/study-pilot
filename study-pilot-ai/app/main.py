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
from app.providers import get_provider, get_provider_chain, get_provider_chain_names
from app.routing import ProviderRouter, ProviderMetrics
from app.routing.config_loader import get_provider_costs
from app.services import ConceptService, QuizService
from app.services.embedding_service import create_embeddings_via_provider
from app.services.tutor_service import tutor_respond, evaluate_exercise

load_dotenv()


def _configure_logging() -> logging.Logger:
    """
    Configure application logging to write to a rotating file (and capture uvicorn logs too).
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
    log_path = Path(os.getenv("AI_LOG_PATH", str(logs_dir / "study-pilot-ai.log")))

    file_handler = RotatingFileHandler(log_path, maxBytes=1_000_000, backupCount=5, encoding="utf-8")
    formatter = logging.Formatter(
        "%(asctime)s [%(levelname)s] %(name)s %(message)s"
    )
    file_handler.setFormatter(formatter)

    # Avoid adding duplicate handlers if this is reloaded (e.g. uvicorn --reload)
    if not any(isinstance(h, RotatingFileHandler) and getattr(h, "baseFilename", "") == str(log_path) for h in root_logger.handlers):
        root_logger.addHandler(file_handler)

    # Ensure uvicorn loggers also write to the same file.
    for name in ("uvicorn", "uvicorn.error", "uvicorn.access"):
        l = logging.getLogger(name)
        l.setLevel(level)
        if not any(isinstance(h, RotatingFileHandler) and getattr(h, "baseFilename", "") == str(log_path) for h in l.handlers):
            l.addHandler(file_handler)
        l.propagate = False

    return logging.getLogger(__name__)


logger = _configure_logging()

_settings: Settings | None = None


def get_settings() -> Settings:
    global _settings
    if _settings is None:
        _settings = Settings()
    return _settings


def _get_provider():
    return get_provider(get_settings())


async def _embed_executor(name: str, texts: list[str]):
    return await create_embeddings_via_provider(name, texts, get_settings())


async def _tutor_runner(provider, payload: dict):
    return await tutor_respond(
        user_message=payload.get("user_message", ""),
        current_step=payload.get("current_step", "Complete"),
        goals=payload.get("goals", []),
        mastery_levels=payload.get("mastery_levels", []),
        recent_mistakes=payload.get("recent_mistakes") or [],
        explanation_style=payload.get("explanation_style"),
        tone=payload.get("tone"),
        retrieved_chunks=payload.get("retrieved_chunks") or [],
        provider=provider,
    )


async def _tutor_eval_runner(provider, payload: dict):
    return await evaluate_exercise(
        question=payload.get("question", ""),
        expected_answer=payload.get("expected_answer", ""),
        user_answer=payload.get("user_answer", ""),
        provider=provider,
    )


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    chain = get_provider_chain(settings)
    names = [n for n, _ in chain]
    logger.info("LLM provider chain (router): %s", ", ".join(names) if names else "none")
    metrics = ProviderMetrics()
    costs = get_provider_costs()
    task_runners = {"tutor": _tutor_runner, "tutor_eval": _tutor_eval_runner}
    router_instance = ProviderRouter(
        chain, metrics, costs, embedding_executor=_embed_executor, task_runners=task_runners
    )
    app.state.router = router_instance
    app.state.concept_service = ConceptService(router_instance)
    app.state.quiz_service = QuizService(router_instance)
    yield


app = FastAPI(lifespan=lifespan)
app.add_exception_handler(Exception, global_exception_handler)
app.include_router(router, prefix="", tags=["ai"])


@app.get("/health")
async def health():
    """Production health: api, embedding, chat_provider, fallback_available. No DB in Python service."""
    settings = get_settings()
    chain = get_provider_chain_names(settings)
    provider = get_provider(settings)
    chat_ok = provider is not None
    fallback_available = len(chain) > 1
    return {
        "api": "ok",
        "embedding": "ok",
        "chat_provider": "ok" if chat_ok else "fail",
        "fallback_available": fallback_available,
        "db": "n/a",
    }
