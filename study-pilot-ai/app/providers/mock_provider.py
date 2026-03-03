"""Mock AI provider for free local testing. No external API calls."""

import json

from app.providers.base import LLMProvider


class MockProvider(LLMProvider):
    """Returns deterministic fake responses. Use AI_MODE=mock for local testing."""

    async def extract_concepts(self, text: str) -> list[dict]:
        return [
            {"name": "Variables", "description": "Storage for data values."},
            {"name": "Loops", "description": "Control flow for repetition."},
            {"name": "Functions", "description": "Reusable blocks of code."},
            {"name": "Object-Oriented Programming", "description": "Programming paradigm using objects."},
            {"name": "Async Programming", "description": "Concurrent execution patterns."},
        ]

    async def generate_questions(self, concepts: list[dict], count: int) -> list[dict]:
        n = min(count, 5)
        questions = [
            {"text": "What are variables used for?", "options": ["Storage", "Display", "Delete", "Compile"], "correctAnswer": "Storage"},
            {"text": "Which construct repeats code?", "options": ["Variable", "Loop", "Function", "Class"], "correctAnswer": "Loop"},
            {"text": "What do functions provide?", "options": ["I/O only", "Reusability", "Storage", "Syntax"], "correctAnswer": "Reusability"},
            {"text": "OOP stands for?", "options": ["Object-Oriented Programming", "Open Output Protocol", "Ordered Operations", "Other"], "correctAnswer": "Object-Oriented Programming"},
            {"text": "Async programming enables?", "options": ["Faster CPU", "Concurrency", "More memory", "Larger files"], "correctAnswer": "Concurrency"},
        ]
        return questions[:n]

    def _is_tutor_prompt(self, system: str) -> bool:
        s = (system or "").lower()
        return "tutor" in s and ("nextstep" in s or "optionalexercise" in s or "return json" in s)

    def _is_evaluate_prompt(self, system: str) -> bool:
        s = (system or "").lower()
        return "evaluating" in s and "iscorrect" in s

    async def chat(
        self,
        system: str,
        question: str,
        context: list[dict],
        explanation_style: str | None = None,
    ) -> dict:
        if self._is_evaluate_prompt(system):
            answer = json.dumps({
                "isCorrect": True,
                "explanation": "Mock: Your answer is correct for E2E testing.",
            })
            return {"answer": answer, "citedChunkIds": []}
        if self._is_tutor_prompt(system):
            answer = json.dumps({
                "message": "Mock tutor response. This is a short explanation for E2E testing.",
                "nextStep": "Explain",
                "optionalExercise": None,
                "citedChunkIds": [],
            })
            return {"answer": answer, "citedChunkIds": []}
        return {
            "answer": "This is a mock chat response. Enable a real LLM provider for RAG answers.",
            "citedChunkIds": [],
        }
