import json
import logging

import httpx
from fastapi import APIRouter, Depends, Request, HTTPException

logger = logging.getLogger(__name__)
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
from app.services.chat_service import chat as run_chat, stream_chat as run_stream_chat

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
    text_len = len(body.text or "")
    logger.info(
        "Document processing (extract-concepts) requested document_id=%s text_len=%d",
        body.document_id,
        text_len,
    )
    try:
        concepts = await service.extract_concepts(body.text)
        logger.info(
            "Document processing (extract-concepts) completed document_id=%s concept_count=%d",
            body.document_id,
            len(concepts or []),
        )
        return ExtractConceptsResponse(concepts=concepts)
    except Exception as exc:
        logger.exception(
            "Document processing (extract-concepts) failed document_id=%s text_len=%d error=%s",
            body.document_id,
            text_len,
            str(exc),
        )
        raise


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
async def embeddings(body: EmbeddingsRequest, request: Request, settings: Settings = Depends(get_settings)):
    texts = body.texts[:256]
    if not texts:
        raise HTTPException(status_code=400, detail="At least one text is required.")
    router = getattr(request.app.state, "router", None)
    if not router:
        raise HTTPException(status_code=503, detail="Router not available.")
    vectors = await router.execute("embeddings", {"texts": texts})
    return EmbeddingsResponse(embeddings=vectors, model=settings.embedding_model or None)


@router.post("/chat", response_model=ChatResponse)
async def chat(body: ChatRequest, request: Request):
    router = getattr(request.app.state, "router", None)
    if not router:
        raise HTTPException(status_code=503, detail="Router not available.")
    context = [{"chunkId": c.chunk_id, "documentId": c.document_id, "text": c.text} for c in (body.context or [])]
    payload = {"system": body.system, "question": body.question, "context": context, "explanation_style": body.explanation_style}
    try:
        result = await router.execute("chat", payload)
        return ChatResponse(
            answer=result.get("answer") or "",
            cited_chunk_ids=result.get("citedChunkIds") or [],
            model=result.get("model"),
            status="ok",
        )
    except Exception as exc:
        logger.exception("Chat failed: %s", exc)
        return ChatResponse(
            answer="I'm temporarily unable to reach the AI provider. Please try again shortly.",
            cited_chunk_ids=[],
            model=None,
            status="error",
        )


@router.post("/chat/stream")
async def chat_stream(body: ChatRequest, request: Request):
    router = getattr(request.app.state, "router", None)
    if not router:
        raise HTTPException(status_code=503, detail="Router not available.")
    context = [{"chunkId": c.chunk_id, "documentId": c.document_id, "text": c.text} for c in (body.context or [])]
    payload = {"system": body.system, "question": body.question, "context": context, "explanation_style": body.explanation_style}

    async def generate():
        try:
            stream_source = router.execute_stream("chat", payload)
            async for event in run_stream_chat(body.system, body.question, context, stream_source=stream_source, explanation_style=body.explanation_style):
                yield json.dumps(event) + "\n"
        except Exception as exc:
            logger.exception("Chat stream failed: %s", exc)
            yield json.dumps({"token": "I'm temporarily unable to reach the AI provider. Please try again shortly."}) + "\n"
            yield json.dumps({"done": True, "citedChunkIds": [], "model": None}) + "\n"

    return StreamingResponse(
        generate(),
        media_type="application/x-ndjson",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


@router.post("/tutor/respond", response_model=TutorResponseOut)
async def tutor_respond_endpoint(body: TutorContextIn, request: Request):
    router = getattr(request.app.state, "router", None)
    if not router:
        raise HTTPException(status_code=503, detail="Router not available.")
    goals = [{"goalId": g.goal_id, "conceptId": g.concept_id, "conceptName": g.concept_name, "goalType": g.goal_type, "progressPercent": g.progress_percent} for g in (body.goals or [])]
    mastery = [{"conceptId": m.concept_id, "conceptName": m.concept_name, "masteryScore": m.mastery_score} for m in (body.mastery_levels or [])]
    chunks = [{"chunkId": c.chunk_id, "documentId": c.document_id, "text": c.text} for c in (body.retrieved_chunks or [])]
    payload = {
        "user_message": body.user_message,
        "current_step": body.current_step,
        "goals": goals,
        "mastery_levels": mastery,
        "recent_mistakes": body.recent_mistakes or [],
        "explanation_style": body.explanation_style,
        "tone": body.tone,
        "retrieved_chunks": chunks,
    }
    try:
        result = await router.execute("tutor", payload)
    except HTTPException:
        raise
    except (httpx.TimeoutException, httpx.ConnectError, ConnectionError, OSError) as exc:
        logger.exception("Tutor respond failed: %s", exc)
        raise HTTPException(status_code=503, detail="Tutor AI service failed. Please try again.") from exc
    except Exception as exc:
        logger.exception("Tutor respond failed for user_message=%s: %s", body.user_message, exc)
        raise HTTPException(status_code=503, detail="Tutor AI service failed. Please try again.") from exc
    ex = result.get("optionalExercise") if isinstance(result, dict) else None
    optional_exercise = None
    if ex and isinstance(ex, dict):
        optional_exercise = TutorExerciseOut(
            question=ex.get("question") or "",
            expected_answer=ex.get("expectedAnswer") or ex.get("expected_answer") or "",
            difficulty=ex.get("difficulty") or "medium",
        )
    next_step_val = result.get("nextStep") or getattr(body, "current_step", None) or "Complete"
    out_message = result.get("message") or ""
    logger.info(
        "Tutor respond OK: message_len=%d next_step=%s has_exercise=%s",
        len(out_message),
        next_step_val,
        optional_exercise is not None,
    )
    return TutorResponseOut(
        message=out_message,
        next_step=next_step_val,
        optional_exercise=optional_exercise,
        cited_chunk_ids=result.get("citedChunkIds") or [],
    )


@router.post("/tutor/evaluate-exercise", response_model=ExerciseEvaluationResponse)
async def evaluate_exercise_endpoint(body: ExerciseEvaluationRequest, request: Request):
    router = getattr(request.app.state, "router", None)
    if not router:
        raise HTTPException(status_code=503, detail="Router not available.")
    payload = {"question": body.question, "expected_answer": body.expected_answer, "user_answer": body.user_answer}
    try:
        result = await router.execute("tutor_eval", payload)
    except HTTPException:
        raise
    except Exception as exc:
        logger.exception("Tutor evaluate-exercise failed: %s", exc)
        raise HTTPException(status_code=503, detail="Tutor AI service failed. Please try again.") from exc
    return ExerciseEvaluationResponse(
        is_correct=result.get("isCorrect", False),
        explanation=result.get("explanation") or "",
    )
