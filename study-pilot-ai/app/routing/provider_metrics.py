import threading
import time
from typing import Any

COOLDOWN_SEC = 60.0
FAILURE_WINDOW_SEC = 60.0
MAX_FAILURES_FOR_COOLDOWN = 3


class ProviderMetrics:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._data: dict[str, dict[str, Any]] = {}

    def _ensure(self, name: str) -> dict[str, Any]:
        if name not in self._data:
            self._data[name] = {
                "avg_latency_ms": 500.0,
                "success_count": 0,
                "failure_count": 0,
                "last_failure_time": 0.0,
                "failure_times": [],
                "cooldown_until": 0.0,
                "tokens_per_second": 0.0,
                "cost_per_1k_tokens": 0.0,
            }
        return self._data[name]

    def record_success(self, name: str, latency_ms: float, tokens: int = 0) -> None:
        with self._lock:
            d = self._ensure(name)
            d["success_count"] = d.get("success_count", 0) + 1
            old_avg = d["avg_latency_ms"]
            n = d["success_count"] + d.get("failure_count", 0)
            d["avg_latency_ms"] = old_avg + (latency_ms - old_avg) / max(1, n)
            if tokens > 0:
                d["tokens_per_second"] = tokens / max(0.001, latency_ms / 1000.0)

    def record_failure(self, name: str, is_429: bool = False, is_503: bool = False, is_timeout: bool = False) -> None:
        with self._lock:
            d = self._ensure(name)
            d["failure_count"] = d.get("failure_count", 0) + 1
            now = time.monotonic()
            d["last_failure_time"] = now
            times = d.get("failure_times", [])
            times.append(now)
            cutoff = now - FAILURE_WINDOW_SEC
            d["failure_times"] = [t for t in times if t > cutoff]
            if len(d["failure_times"]) >= MAX_FAILURES_FOR_COOLDOWN or is_429 or is_503 or is_timeout:
                d["cooldown_until"] = now + COOLDOWN_SEC

    def get_avg_latency_ms(self, name: str) -> float:
        with self._lock:
            return self._ensure(name).get("avg_latency_ms", 500.0)

    def get_success_rate(self, name: str) -> float:
        with self._lock:
            d = self._ensure(name)
            s = d.get("success_count", 0)
            f = d.get("failure_count", 0)
            total = s + f
            return s / total if total > 0 else 1.0

    def get_error_rate(self, name: str) -> float:
        with self._lock:
            d = self._ensure(name)
            s = d.get("success_count", 0)
            f = d.get("failure_count", 0)
            total = s + f
            return f / total if total > 0 else 0.0

    def get_cooldown_until(self, name: str) -> float:
        with self._lock:
            return self._ensure(name).get("cooldown_until", 0.0)

    def is_healthy(self, name: str) -> bool:
        with self._lock:
            d = self._ensure(name)
            return time.monotonic() >= d.get("cooldown_until", 0.0)

    def set_cost_per_1k(self, name: str, cost: float) -> None:
        with self._lock:
            self._ensure(name)["cost_per_1k_tokens"] = cost

    def get_cost_per_1k(self, name: str) -> float:
        with self._lock:
            return self._ensure(name).get("cost_per_1k_tokens", 0.0)

    def get_tokens_per_second(self, name: str) -> float:
        with self._lock:
            return self._ensure(name).get("tokens_per_second", 0.0)

    def get_all_names(self) -> list[str]:
        with self._lock:
            return list(self._data.keys())

    def snapshot(self, name: str) -> dict[str, Any]:
        with self._lock:
            d = self._ensure(name).copy()
            d["healthy"] = time.monotonic() >= d.get("cooldown_until", 0.0)
            return d
