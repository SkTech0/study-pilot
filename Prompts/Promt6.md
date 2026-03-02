You are building STEP 6 — AI Service.

Create a production-ready Python AI microservice for StudyPilot.

====================================================
GLOBAL RULES
====================================================

- Python 3.12+
- FastAPI latest stable
- Pydantic v2
- Async-first design
- No notebooks
- No demo scripts
- Production folder structure
- Minimal explanations, code only

====================================================
SERVICE PURPOSE
====================================================

Provide AI capabilities for StudyPilot backend:

1. Extract concepts from study content
2. Generate quiz questions
3. Provide future-ready LLM abstraction

This service is stateless.

====================================================
PROJECT STRUCTURE
====================================================

study-pilot-ai/
 ├── app/
 │    ├── api/
 │    ├── core/
 │    ├── services/
 │    ├── providers/
 │    ├── models/
 │    ├── prompts/
 │    └── main.py
 ├── tests/
 └── pyproject.toml

====================================================
DEPENDENCIES
====================================================

Use:

- fastapi
- uvicorn
- pydantic
- httpx
- python-dotenv
- tenacity (retry logic)

Prepare structure for LangChain later but DO NOT add yet.

====================================================
API CONTRACT
====================================================

POST /extract-concepts

Request:
{
  "documentId": "guid",
  "text": "string"
}

Response:
{
  "concepts": [
    { "name": "", "description": "" }
  ]
}

----------------------------------------------------

POST /generate-quiz

Request:
{
  "documentId": "guid",
  "concepts": [
    { "name": "" }
  ],
  "questionCount": 5
}

Response:
{
  "questions": [
    {
      "text": "",
      "options": [],
      "correctAnswer": ""
    }
  ]
}

====================================================
ARCHITECTURE
====================================================

Controller → Service → Provider

Controllers must NOT contain AI logic.

====================================================
LLM ABSTRACTION
====================================================

Create base interface:

LLMProvider:

async extract_concepts(text)
async generate_questions(concepts, count)

====================================================
OPENAI PROVIDER
====================================================

Implement OpenAIProvider:

- uses httpx AsyncClient
- reads API key from environment
- retry using tenacity
- timeout configured

Do NOT hardcode prompts.

====================================================
PROMPT MANAGEMENT
====================================================

Create prompts module:

extract_concepts_prompt.py
generate_quiz_prompt.py

Prompts returned by functions.

====================================================
SERVICE LAYER
====================================================

ConceptService
QuizService

Responsible for calling provider and shaping responses.

====================================================
CONFIGURATION
====================================================

Use Settings class via Pydantic:

- OPENAI_API_KEY
- MODEL_NAME
- REQUEST_TIMEOUT

Load from .env.

====================================================
ERROR HANDLING
====================================================

Global exception handler returning structured JSON errors.

====================================================
HEALTH CHECK
====================================================

GET /health → {"status":"ok"}

====================================================
OUTPUT RULE
====================================================

Generate full service code.

Do NOT add README.
Do NOT add explanations.

Stop after project scaffold is complete.