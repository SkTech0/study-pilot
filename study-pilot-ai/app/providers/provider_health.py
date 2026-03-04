"""Provider health registry: mark providers unavailable (e.g. 402) for a cooldown period."""
import logging
import threading
import time
from typing import Dict

logger = logging.getLogger(__name__)

# Provider name -> Unix timestamp until which the provider is considered unavailable
_unavailable: Dict[str, float] = {}
_lock = threading.Lock()
DEFAULT_COOLDOWN_SEC = 300  # 5 minutes


def mark_unavailable(provider_name: str, cooldown_sec: float = DEFAULT_COOLDOWN_SEC) -> None:
    until = time.monotonic() + cooldown_sec
    with _lock:
        _unavailable[provider_name] = until
    logger.warning("Provider %s marked unavailable for %.0fs", provider_name, cooldown_sec)


def is_available(provider_name: str) -> bool:
    now = time.monotonic()
    with _lock:
        until = _unavailable.get(provider_name, 0)
        if until > 0 and now < until:
            return False
        if until > 0:
            del _unavailable[provider_name]
    return True


def clear_unavailable(provider_name: str | None = None) -> None:
    with _lock:
        if provider_name is None:
            _unavailable.clear()
        elif provider_name in _unavailable:
            del _unavailable[provider_name]
