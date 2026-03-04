import os
from pathlib import Path
from typing import Any

try:
    import yaml
except ImportError:
    yaml = None


def load_provider_config() -> dict[str, dict[str, Any]]:
    out: dict[str, dict[str, Any]] = {}
    if not yaml:
        return out
    base = Path(__file__).resolve().parent.parent.parent
    for candidate in (base / "config" / "providers.yaml", Path("config/providers.yaml"), Path("providers.yaml")):
        if candidate.is_file():
            try:
                with open(candidate, encoding="utf-8") as f:
                    data = yaml.safe_load(f) or {}
                providers = data.get("providers") or {}
                for name, cfg in providers.items():
                    if isinstance(cfg, dict):
                        out[str(name).lower()] = {"cost": float(cfg.get("cost", 0)), "timeout_ms": int(cfg.get("timeout_ms", 30000))}
            except Exception:
                pass
            break
    env_config = os.environ.get("PROVIDERS_CONFIG_PATH")
    if env_config and Path(env_config).is_file():
        try:
            with open(env_config, encoding="utf-8") as f:
                data = yaml.safe_load(f) or {}
            providers = data.get("providers") or {}
            for name, cfg in providers.items():
                if isinstance(cfg, dict):
                    out[str(name).lower()] = {"cost": float(cfg.get("cost", 0)), "timeout_ms": int(cfg.get("timeout_ms", 30000))}
        except Exception:
            pass
    return out


def get_provider_costs(config: dict[str, dict[str, Any]] | None = None) -> dict[str, float]:
    if config is None:
        config = load_provider_config()
    return {k: v.get("cost", 0.0) for k, v in config.items()}
