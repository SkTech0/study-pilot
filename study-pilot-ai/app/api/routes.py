from fastapi import APIRouter, Depends, Request, HTTPException
from fastapi.responses import JSONResponse

from app.core.config import Settings
from app.models.schemas import (
    ChatRequest,
    ChatResponse,
    EmbeddingsRequest,
    EmbeddingsResponse,
    ExtractConceptsRequest,
    ExtractConceptsResponse,
    GenerateQuizRequest,
    GenerateQuizResponse,
)
from app.services import ConceptService, QuizService
from app.services.embedding_service import create_embeddings
from app.services.chat_service import chat as run_chat

router = APIRouter()


def get_concept_service(request: Request) -> ConceptService:
    return request.app.state.concept_service


def get_quiz_service(request: Request) -> QuizService:
    return request.app.state.quiz_service


def get_settings() -> Settings:
    return Settings()


@router.post("/extract-concepts", response_model=ExtractConceptsResponse)
async def extract_concepts(
    body: ExtractConceptsRequest,
    service: ConceptService = Depends(get_concept_service),
):
    concepts = await service.extract_concepts(body.text)
    return ExtractConceptsResponse(concepts=concepts)


@router.post("/generate-quiz", response_model=GenerateQuizResponse)
async def generate_quiz(
    body: GenerateQuizRequest,
    service: QuizService = Depends(get_quiz_service),
):
    count = max(1, min(body.question_count or 3, 10))
    questions = await service.generate_questions(body.concepts, count)
    # If LLM returned truncated/invalid JSON (0 questions), retry once with fewer items
    if not questions and count > 1:
        questions = await service.generate_questions(body.concepts, 1)
    if not questions:
        raise HTTPException(
            status_code=503,
            detail="Quiz generation produced no questions. The AI service may be unavailable or rate-limited. Please try again.",
        )
    response = GenerateQuizResponse(questions=questions)
    return JSONResponse(content=response.model_dump(mode="json", by_alias=True))


@router.post("/embeddings", response_model=EmbeddingsResponse)
async def embeddings(body: EmbeddingsRequest, settings: Settings = Depends(get_settings)):
    texts = body.texts[:256]
    if not texts:
        raise HTTPException(status_code=400, detail="At least one text is required.")
    vectors = await create_embeddings(texts, settings)
    return EmbeddingsResponse(embeddings=vectors, model=settings.embedding_model or None)


@router.post("/chat", response_model=ChatResponse)
async def chat(
    body: ChatRequest,
    request: Request,
):
    provider = getattr(request.app.state, "llm_provider", None)
    if provider is None:
        raise HTTPException(status_code=503, detail="LLM provider not available.")
    context = [{"chunkId": c.chunk_id, "documentId": c.document_id, "text": c.text} for c in (body.context or [])]
    result = await run_chat(body.system, body.question, context, provider)
    return ChatResponse(
        answer=result["answer"],
        cited_chunk_ids=result["citedChunkIds"],
        model=result.get("model"),
    )
