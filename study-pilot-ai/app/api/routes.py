import json

from fastapi import APIRouter, Depends, Request, HTTPException
from fastapi.responses import JSONResponse, StreamingResponse

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
    TutorContextIn,
    TutorResponseOut,
    TutorExerciseOut,
    ExerciseEvaluationRequest,
    ExerciseEvaluationResponse,
)
from app.services import ConceptService, QuizService
from app.services.embedding_service import create_embeddings
from app.services.chat_service import chat as run_chat, stream_chat as run_stream_chat
from app.services.tutor_service import tutor_respond

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
    result = await run_chat(
        body.system, body.question, context, provider, body.explanation_style
    )
    return ChatResponse(
        answer=result["answer"],
        cited_chunk_ids=result["citedChunkIds"],
        model=result.get("model"),
    )


@router.post("/chat/stream")
async def chat_stream(
    body: ChatRequest,
    request: Request,
):
    provider = getattr(request.app.state, "llm_provider", None)
    if provider is None:
        raise HTTPException(status_code=503, detail="LLM provider not available.")
    context = [{"chunkId": c.chunk_id, "documentId": c.document_id, "text": c.text} for c in (body.context or [])]

    async def generate():
        async for event in run_stream_chat(
            body.system, body.question, context, provider, body.explanation_style
        ):
            yield json.dumps(event) + "\n"

    return StreamingResponse(
        generate(),
        media_type="application/x-ndjson",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


@router.post("/tutor/respond", response_model=TutorResponseOut)
async def tutor_respond_endpoint(
    body: TutorContextIn,
    request: Request,
):
    provider = getattr(request.app.state, "llm_provider", None)
    if provider is None:
        raise HTTPException(status_code=503, detail="LLM provider not available.")
    goals = [{"goalId": g.goal_id, "conceptId": g.concept_id, "conceptName": g.concept_name, "goalType": g.goal_type, "progressPercent": g.progress_percent} for g in (body.goals or [])]
    mastery = [{"conceptId": m.concept_id, "conceptName": m.concept_name, "masteryScore": m.mastery_score} for m in (body.mastery_levels or [])]
    chunks = [{"chunkId": c.chunk_id, "documentId": c.document_id, "text": c.text} for c in (body.retrieved_chunks or [])]
    result = await tutor_respond(
        user_message=body.user_message,
        current_step=body.current_step,
        goals=goals,
        mastery_levels=mastery,
        recent_mistakes=body.recent_mistakes or [],
        explanation_style=body.explanation_style,
        tone=body.tone,
        retrieved_chunks=chunks,
        provider=provider,
    )
    ex = result.get("optionalExercise")
    optional_exercise = None
    if ex and isinstance(ex, dict):
        optional_exercise = TutorExerciseOut(
            question=ex.get("question") or "",
            expected_answer=ex.get("expectedAnswer") or ex.get("expected_answer") or "",
            difficulty=ex.get("difficulty") or "medium",
        )
    return TutorResponseOut(
        message=result.get("message") or "",
        next_step=result.get("nextStep") or body.current_step,
        optional_exercise=optional_exercise,
        cited_chunk_ids=result.get("citedChunkIds") or [],
    )


@router.post("/tutor/evaluate-exercise", response_model=ExerciseEvaluationResponse)
async def evaluate_exercise_endpoint(
    body: ExerciseEvaluationRequest,
    request: Request,
):
    provider = getattr(request.app.state, "llm_provider", None)
    if provider is None:
        raise HTTPException(status_code=503, detail="LLM provider not available.")
    from app.services.tutor_service import evaluate_exercise as run_evaluate
    result = await run_evaluate(
        question=body.question,
        expected_answer=body.expected_answer,
        user_answer=body.user_answer,
        provider=provider,
    )
    return ExerciseEvaluationResponse(
        is_correct=result.get("isCorrect", False),
        explanation=result.get("explanation") or "",
    )
