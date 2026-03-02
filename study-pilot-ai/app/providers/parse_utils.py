"""Shared JSON parsing for LLM responses (fence stripping, truncation repair)."""
import json
import logging
import re

logger = logging.getLogger(__name__)


def _strip_fences(raw: str) -> str:
    raw = raw.strip()
    if raw.startswith("```"):
        lines = raw.split("\n")
        raw = "\n".join(lines[1:-1]) if lines[0].startswith("```json") else "\n".join(lines[1:-1])
    return raw.strip()


def _repair_truncated_json_array(raw: str) -> str | None:
    """If the LLM response was truncated, try to close at last complete object."""
    if not raw.strip():
        return None
    s = raw.strip()
    if not s.startswith("["):
        s = "[" + s

    # Prefer last object boundary "}\s*," so we cut between complete objects
    for m in reversed(list(re.finditer(r"}\s*,", s))):
        candidate = s[: m.start() + 1] + "]"
        try:
            parsed = json.loads(candidate)
            if isinstance(parsed, list):
                return candidate
        except json.JSONDecodeError:
            continue

    # Fallback: any "}" that yields valid JSON (handles trailing "}" without comma)
    for i in range(len(s) - 1, -1, -1):
        if s[i] == "}":
            candidate = s[: i + 1]
            if not candidate.rstrip().endswith("]"):
                candidate += "]"
            try:
                parsed = json.loads(candidate)
                if isinstance(parsed, list):
                    return candidate
            except json.JSONDecodeError:
                continue
    return None


def parse_json_array(raw: str) -> list[dict]:
    raw = _strip_fences(raw)
    try:
        out = json.loads(raw)
        return out if isinstance(out, list) else [out] if isinstance(out, dict) else []
    except json.JSONDecodeError as e:
        repaired = _repair_truncated_json_array(raw)
        if repaired:
            try:
                out = json.loads(repaired)
                if isinstance(out, list):
                    logger.warning(
                        "LLM JSON was truncated; used %d complete item(s).", len(out)
                    )
                    return out
            except json.JSONDecodeError:
                pass
        logger.warning(
            "Could not parse JSON from LLM: %s. Returning empty list.", e
        )
        return []
