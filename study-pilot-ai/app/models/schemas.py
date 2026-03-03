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
    explanation_style: str | None = Field(None, alias="explanationStyle")


class ChatResponse(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    answer: str
    cited_chunk_ids: list[str] = Field(default_factory=list, alias="citedChunkIds")
    model: str | None = None


class TutorGoalIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    goal_id: str = Field(..., alias="goalId")
    concept_id: str = Field(..., alias="conceptId")
    concept_name: str = Field(..., alias="conceptName")
    goal_type: str = Field(..., alias="goalType")
    progress_percent: int = Field(0, alias="progressPercent")


class TutorMasteryIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    concept_id: str = Field(..., alias="conceptId")
    concept_name: str = Field(..., alias="conceptName")
    mastery_score: int = Field(0, alias="masteryScore")


class TutorChunkIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    chunk_id: str = Field(..., alias="chunkId")
    document_id: str = Field(..., alias="documentId")
    text: str


class TutorContextIn(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    user_id: str = Field(..., alias="userId")
    tutor_session_id: str = Field(..., alias="tutorSessionId")
    user_message: str = Field(..., alias="userMessage")
    current_step: str = Field(..., alias="currentStep")
    goals: list[TutorGoalIn] = Field(default_factory=list)
    mastery_levels: list[TutorMasteryIn] = Field(default_factory=list, alias="masteryLevels")
    recent_mistakes: list[str] = Field(default_factory=list, alias="recentMistakes")
    explanation_style: str | None = Field(None, alias="explanationStyle")
    tone: str | None = None
    retrieved_chunks: list[TutorChunkIn] = Field(default_factory=list, alias="retrievedChunks")


class TutorExerciseOut(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    question: str
    expected_answer: str = Field(..., alias="expectedAnswer")
    difficulty: str = "medium"


class TutorResponseOut(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    message: str
    next_step: str = Field(..., alias="nextStep")
    optional_exercise: TutorExerciseOut | None = Field(None, alias="optionalExercise")
    cited_chunk_ids: list[str] = Field(default_factory=list, alias="citedChunkIds")


class ExerciseEvaluationRequest(BaseModel):
    model_config = ConfigDict(populate_by_name=True)
    exercise_id: str = Field(..., alias="exerciseId")
    question: str
    expected_answer: str = Field(..., alias="expectedAnswer")
    user_answer: str = Field(..., alias="userAnswer")


class ExerciseEvaluationResponse(BaseModel):
    model_config = ConfigDict(serialize_by_alias=True)
    is_correct: bool = Field(..., alias="isCorrect")
    explanation: str
