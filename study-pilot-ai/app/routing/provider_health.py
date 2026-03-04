from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from app.routing.provider_metrics import ProviderMetrics

LATENCY_WEIGHT = 0.35
RELIABILITY_WEIGHT = 0.45
COST_WEIGHT = 0.2
MAX_LATENCY_MS = 30000.0
MAX_COST = 10.0


def compute_score(
    metrics: ProviderMetrics,
    name: str,
    latency_weight: float = LATENCY_WEIGHT,
    reliability_weight: float = RELIABILITY_WEIGHT,
    cost_weight: float = COST_WEIGHT,
) -> float:
    if not metrics.is_healthy(name):
        return float("inf")
    lat = metrics.get_avg_latency_ms(name)
    fail_ratio = metrics.get_error_rate(name)
    cost = metrics.get_cost_per_1k(name)
    norm_lat = min(1.0, lat / MAX_LATENCY_MS)
    norm_cost = min(1.0, cost / MAX_COST)
    return (
        latency_weight * norm_lat
        + reliability_weight * fail_ratio
        + cost_weight * norm_cost
    )


def rank_providers(
    metrics: ProviderMetrics,
    names: list[str],
    latency_weight: float = LATENCY_WEIGHT,
    reliability_weight: float = RELIABILITY_WEIGHT,
    cost_weight: float = COST_WEIGHT,
) -> list[tuple[str, float]]:
    scored = [
        (n, compute_score(metrics, n, latency_weight, reliability_weight, cost_weight))
        for n in names
    ]
    return sorted(scored, key=lambda x: x[1])
