from __future__ import annotations

from typing import TYPE_CHECKING

from app.routing.provider_health import rank_providers

if TYPE_CHECKING:
    from app.routing.provider_metrics import ProviderMetrics

EMBEDDING_CAPABLE = frozenset({"ollama", "openrouter", "openai"})


def _weights(task_type: str) -> tuple[float, float, float]:
    w = {
        "chat": (0.5, 0.35, 0.15),
        "stream": (0.6, 0.3, 0.1),
        "tutor": (0.25, 0.55, 0.2),
        "tutor_eval": (0.25, 0.55, 0.2),
        "extract": (0.2, 0.3, 0.5),
        "summary": (0.33, 0.33, 0.34),
        "quiz": (0.33, 0.33, 0.34),
        "embeddings": (0.1, 0.3, 0.6),
    }
    return w.get(task_type, (0.33, 0.33, 0.34))


def select_provider(
    task_type: str,
    metrics: ProviderMetrics,
    provider_names: list[str],
    provider_costs: dict[str, float] | None = None,
) -> str | None:
    for name in provider_names:
        metrics.set_cost_per_1k(name, (provider_costs or {}).get(name, 0.0))
    names = list(provider_names)
    if task_type == "embeddings":
        names = [n for n in names if n in EMBEDDING_CAPABLE]
    if not names:
        return None
    lat_w, rel_w, cost_w = _weights(task_type)
    ranked = rank_providers(metrics, names, lat_w, rel_w, cost_w)
    for name, score in ranked:
        if metrics.is_healthy(name) and score != float("inf"):
            return name
    return ranked[0][0] if ranked else None


def select_provider_ranked(
    task_type: str,
    metrics: ProviderMetrics,
    provider_names: list[str],
    provider_costs: dict[str, float] | None = None,
) -> list[str]:
    for name in provider_names:
        metrics.set_cost_per_1k(name, (provider_costs or {}).get(name, 0.0))
    names = list(provider_names)
    if task_type == "embeddings":
        names = [n for n in names if n in EMBEDDING_CAPABLE]
    if not names:
        return []
    lat_w, rel_w, cost_w = _weights(task_type)
    ranked = rank_providers(metrics, names, lat_w, rel_w, cost_w)
    return [n for n, _ in ranked]
