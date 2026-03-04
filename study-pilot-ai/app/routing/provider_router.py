import logging
import time
from typing import Any, AsyncIterator, Callable, Awaitable

import httpx
from tenacity import RetryError

from app.providers.base import LLMProvider
from app.routing.provider_metrics import ProviderMetrics
from app.routing.routing_policy import select_provider_ranked

logger = logging.getLogger(__name__)

SAFE_FALLBACK_ANSWER = "I'm temporarily unable to reach the AI provider. Please try again shortly."
MAX_RETRIES = 2


def _is_429(exc: Exception) -> bool:
    if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code == 429:
        return True
    if isinstance(exc, RetryError) and exc.last_attempt and getattr(exc.last_attempt, "failed", False):
        try:
            return _is_429(exc.last_attempt.exception())
        except Exception:
            pass
    return False


def _is_503(exc: Exception) -> bool:
    if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code == 503:
        return True
    if isinstance(exc, RetryError) and exc.last_attempt and getattr(exc.last_attempt, "failed", False):
        try:
            return _is_503(exc.last_attempt.exception())
        except Exception:
            pass
    return False


def _is_timeout(exc: Exception) -> bool:
    if isinstance(exc, (httpx.TimeoutException, httpx.ConnectError)):
        return True
    if isinstance(exc, RetryError) and exc.last_attempt and getattr(exc.last_attempt, "failed", False):
        try:
            return _is_timeout(exc.last_attempt.exception())
        except Exception:
            pass
    return False


EmbeddingExecutor = Callable[[str, list[str]], Awaitable[list[list[float]]]]
TaskRunner = Callable[[Any, dict], Awaitable[Any]]


class ProviderRouter:
    def __init__(
        self,
        providers: list[tuple[str, LLMProvider]],
        metrics: ProviderMetrics,
        provider_costs: dict[str, float],
        embedding_executor: EmbeddingExecutor | None = None,
        task_runners: dict[str, TaskRunner] | None = None,
    ) -> None:
        self._providers = {n: p for n, p in providers}
        self._names = [n for n, _ in providers]
        self._metrics = metrics
        self._provider_costs = provider_costs
        self._embedding_executor = embedding_executor
        self._task_runners = task_runners or {}

    def _ranked(self, task_type: str) -> list[str]:
        start = time.perf_counter()
        ranked = select_provider_ranked(task_type, self._metrics, self._names, self._provider_costs)
        elapsed_ms = (time.perf_counter() - start) * 1000
        if elapsed_ms > 5:
            logger.info("ROUTER_EXECUTION_TIME_MS decision_ms=%.2f", elapsed_ms)
        return ranked

    async def _run_llm(self, name: str, task_type: str, payload: dict) -> Any:
        provider = self._providers.get(name)
        if not provider:
            raise ValueError(f"Unknown provider: {name}")
        if task_type == "chat":
            return await provider.chat(
                system=payload.get("system", ""),
                question=payload.get("question", ""),
                context=payload.get("context", []),
                explanation_style=payload.get("explanation_style"),
                require_json=payload.get("require_json", True),
            )
        if task_type == "stream":
            return await provider.stream_chat(
                system=payload.get("system", ""),
                question=payload.get("question", ""),
                context=payload.get("context", []),
                explanation_style=payload.get("explanation_style"),
            )
        if task_type in ("tutor", "tutor_eval"):
            runner = self._task_runners.get(task_type)
            if runner:
                return await runner(provider, payload)
            return await provider.chat(
                system=payload.get("system", ""),
                question=payload.get("question", ""),
                context=payload.get("context", []),
                explanation_style=None,
            )
        if task_type == "extract":
            return await provider.extract_concepts(payload.get("text", ""))
        if task_type in ("summary", "quiz"):
            return await provider.generate_questions(
                payload.get("concepts", []),
                payload.get("count", 1),
            )
        raise ValueError(f"Unknown task_type: {task_type}")

    async def execute(self, task_type: str, payload: dict) -> Any:
        if task_type == "embeddings":
            return await self._execute_embeddings(payload)
        ranked = self._ranked(task_type)
        if not ranked:
            if task_type == "chat":
                return {"answer": SAFE_FALLBACK_ANSWER, "citedChunkIds": [], "model": None}
            if task_type in ("extract", "quiz", "summary"):
                raise RuntimeError("No provider available")
            return {"answer": SAFE_FALLBACK_ANSWER, "citedChunkIds": [], "model": None}
        last_exc: Exception | None = None
        attempts = 0
        for name in ranked:
            if attempts > MAX_RETRIES:
                break
            if not self._metrics.is_healthy(name):
                continue
            # select_provider_ranked already returned the ranking order used above.
            # Recomputing scores here is unnecessary for routing; we log a neutral score.
            score = 0.0
            logger.info("ROUTER_SELECTED provider=%s score=%.4f", name, score)
            t0 = time.perf_counter()
            try:
                result = await self._run_llm(name, task_type, payload)
                latency_ms = (time.perf_counter() - t0) * 1000
                tokens = len(str(result).split()) if isinstance(result, dict) else 0
                self._metrics.record_success(name, latency_ms, tokens)
                return result
            except Exception as e:
                last_exc = e
                is_4 = _is_429(e)
                is_5 = _is_503(e)
                is_to = _is_timeout(e)
                self._metrics.record_failure(name, is_429=is_4, is_503=is_5, is_timeout=is_to)
                if is_4 or is_5 or is_to:
                    logger.warning("ROUTER_COOLDOWN provider=%s", name)
                if name != ranked[-1]:
                    next_name = None
                    for n in ranked:
                        if n != name and self._metrics.is_healthy(n):
                            next_name = n
                            break
                    if next_name:
                        logger.warning("ROUTER_FAILOVER from=%s to=%s", name, next_name)
                attempts += 1
        if task_type == "chat":
            return {"answer": SAFE_FALLBACK_ANSWER, "citedChunkIds": [], "model": None}
        if task_type == "tutor":
            return {"message": SAFE_FALLBACK_ANSWER, "nextStep": "Complete", "optionalExercise": None, "citedChunkIds": []}
        if task_type == "tutor_eval":
            return {"isCorrect": False, "explanation": SAFE_FALLBACK_ANSWER}
        if last_exc:
            raise last_exc
        raise RuntimeError("No provider available")

    async def _execute_embeddings(self, payload: dict) -> list[list[float]]:
        texts = payload.get("texts", [])
        if not self._embedding_executor:
            raise RuntimeError("Embedding executor not configured")
        ranked = self._ranked("embeddings")
        last_exc: Exception | None = None
        for name in ranked:
            if not self._metrics.is_healthy(name):
                continue
            logger.info("ROUTER_SELECTED provider=%s task_type=embeddings", name)
            t0 = time.perf_counter()
            try:
                result = await self._embedding_executor(name, texts)
                latency_ms = (time.perf_counter() - t0) * 1000
                self._metrics.record_success(name, latency_ms)
                return result
            except Exception as e:
                last_exc = e
                is_4 = _is_429(e)
                is_5 = _is_503(e)
                is_to = _is_timeout(e)
                self._metrics.record_failure(name, is_429=is_4, is_503=is_5, is_timeout=is_to)
                if is_4 or is_5 or is_to:
                    logger.warning("ROUTER_COOLDOWN provider=%s", name)
                logger.warning("ROUTER_FAILOVER from=%s task_type=embeddings", name)
        if last_exc:
            raise last_exc
        raise RuntimeError("No embedding provider available")

    async def execute_stream(self, task_type: str, payload: dict) -> AsyncIterator[Any]:
        ranked = self._ranked(task_type)
        if not ranked:
            yield SAFE_FALLBACK_ANSWER
            return
        name = None
        for n in ranked:
            if self._metrics.is_healthy(n):
                name = n
                break
        if name is None:
            yield SAFE_FALLBACK_ANSWER
            return
        # Streaming path uses the same ranked list; avoid recomputing scores here.
        score = 0.0
        logger.info("ROUTER_SELECTED provider=%s score=%.4f stream=true", name, score)
        provider = self._providers.get(name)
        if not provider:
            yield SAFE_FALLBACK_ANSWER
            return
        t0 = time.perf_counter()
        token_count = 0
        try:
            async for token in provider.stream_chat(
                payload.get("system", ""),
                payload.get("question", ""),
                payload.get("context", []),
                payload.get("explanation_style"),
            ):
                token_count += 1
                yield token
            latency_ms = (time.perf_counter() - t0) * 1000
            self._metrics.record_success(name, latency_ms, token_count)
        except Exception as e:
            self._metrics.record_failure(name, is_429=_is_429(e), is_503=_is_503(e), is_timeout=_is_timeout(e))
            logger.warning("ROUTER_COOLDOWN provider=%s", name)
            yield SAFE_FALLBACK_ANSWER
