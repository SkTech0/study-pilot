from pydantic import BaseModel, ConfigDict, Field


class ExtractConceptsRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    document_id: str = Field(..., alias="documentId")
    text: str


class ConceptOut(BaseModel):
    name: str
    description: str | None = None


class ExtractConceptsResponse(BaseModel):
    concepts: list[ConceptOut]


class ConceptIn(BaseModel):
    name: str


class GenerateQuizRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    document_id: str = Field(..., alias="documentId")
    concepts: list[ConceptIn]
    question_count: int = Field(5, alias="questionCount", ge=1, le=20)


class QuizQuestionOut(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    text: str
    options: list[str]
    correct_answer: str = Field("", alias="correctAnswer")


class GenerateQuizResponse(BaseModel):
    questions: list[QuizQuestionOut]
