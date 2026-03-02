from contextlib import asynccontextmanager

from dotenv import load_dotenv
from fastapi import FastAPI

from app.api.routes import router
from app.core.config import Settings
from app.core.exceptions import global_exception_handler
from app.services import ConceptService, QuizService
from app.providers import get_provider

load_dotenv()

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
    provider = get_provider(get_settings())
    app.state.concept_service = ConceptService(provider)
    app.state.quiz_service = QuizService(provider)
    yield


app = FastAPI(lifespan=lifespan)
app.add_exception_handler(Exception, global_exception_handler)
app.include_router(router, prefix="", tags=["ai"])


@app.get("/health")
async def health():
    return {"status": "ok"}
