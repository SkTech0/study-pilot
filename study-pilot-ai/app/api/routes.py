from fastapi import APIRouter, Depends, Request

from app.models.schemas import (
    ExtractConceptsRequest,
    ExtractConceptsResponse,
    GenerateQuizRequest,
    GenerateQuizResponse,
)
from app.services import ConceptService, QuizService

router = APIRouter()


def get_concept_service(request: Request) -> ConceptService:
    return request.app.state.concept_service


def get_quiz_service(request: Request) -> QuizService:
    return request.app.state.quiz_service


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
    questions = await service.generate_questions(body.concepts, body.question_count)
    return GenerateQuizResponse(questions=questions)
