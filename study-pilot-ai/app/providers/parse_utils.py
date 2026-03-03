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
    if not raw or not raw.strip():
        logger.warning("LLM returned empty response. Returning empty list.")
        return []
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
        # Fallback: response may have leading text (e.g. "Here is the JSON: [...]")
        start = raw.find("[")
        if start != -1:
            depth = 0
            for i in range(start, len(raw)):
                if raw[i] == "[":
                    depth += 1
                elif raw[i] == "]":
                    depth -= 1
                    if depth == 0:
                        try:
                            out = json.loads(raw[start : i + 1])
                            if isinstance(out, list):
                                logger.warning("Extracted JSON array from LLM response with surrounding text.")
                                return out
                        except json.JSONDecodeError:
                            pass
                        break
        logger.warning(
            "Could not parse JSON from LLM: %s. Returning empty list.", e
        )
        return []


def parse_json_object(raw: str) -> dict:
    raw = _strip_fences(raw)
    try:
        out = json.loads(raw)
        if isinstance(out, dict):
            return out
        if isinstance(out, list) and out and isinstance(out[0], dict):
            return out[0]
        return {}
    except json.JSONDecodeError as e:
        # Best-effort repair: cut at last closing brace
        s = raw.strip()
        last = s.rfind("}")
        if last != -1:
            candidate = s[: last + 1]
            try:
                out = json.loads(candidate)
                if isinstance(out, dict):
                    logger.warning("LLM JSON object was truncated; repaired using last brace.")
                    return out
            except json.JSONDecodeError:
                pass
        logger.warning("Could not parse JSON object from LLM: %s. Returning empty object.", e)
        return {}
