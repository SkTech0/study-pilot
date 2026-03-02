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
    model_config = ConfigDict(serialize_by_alias=True, populate_by_name=True)
    text: str
    options: list[str]
    correct_answer: str = Field("", alias="correctAnswer")


class GenerateQuizResponse(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    questions: list[QuizQuestionOut]


class EmbeddingsRequest(BaseModel):
    texts: list[str] = Field(default_factory=list, min_length=1, max_length=256)


class EmbeddingsResponse(BaseModel):
    embeddings: list[list[float]]
    model: str | None = None


class ChatContextChunkIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    chunk_id: str = Field(..., alias="chunkId")
    document_id: str = Field(..., alias="documentId")
    text: str


class ChatRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    session_id: str = Field(..., alias="sessionId")
    user_id: str = Field(..., alias="userId")
    document_id: str | None = Field(None, alias="documentId")
    system: str
    question: str
    context: list[ChatContextChunkIn] = Field(default_factory=list)


class ChatResponse(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    answer: str
    cited_chunk_ids: list[str] = Field(default_factory=list, alias="citedChunkIds")
    model: str | None = None
