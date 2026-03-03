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
    """Legacy JSON object parser used for non-chat flows.

    Returns {} on failure. For chat responses prefer safe_parse_llm_json(), which always
    normalizes to a stable schema.
    """
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


def _normalize_chat_object(obj: dict, raw_fallback: str | None = None) -> dict:
    """Normalize varied LLM JSON shapes into the stable chat contract."""
    answer = obj.get("answer")
    if not isinstance(answer, str):
        if answer is None:
            answer = raw_fallback or ""
        else:
            answer = str(answer)
    answer = answer.strip()

    cited = (
        obj.get("citedChunkIds")
        or obj.get("cited_chunk_ids")
        or obj.get("cited_chunks")
        or []
    )
    if not isinstance(cited, list):
        cited = []
    cited_ids = [str(x).strip() for x in cited if isinstance(x, (str, int)) and str(x).strip()]

    return {"answer": answer, "citedChunkIds": cited_ids}


def safe_parse_llm_json(raw: str) -> dict:
    """Parse chat-style LLM output into a robust JSON schema.

    Guarantees:
    - Always returns a dict with keys:
        - answer: str
        - citedChunkIds: list[str]
    - Never raises, never returns {}.
    Parsing strategy:
    1) Try strict JSON (after stripping fences).
    2) Try to extract the first {...} JSON object substring.
    3) Fallback: treat entire content as plain-text answer.
    """
    if raw is None:
        logger.warning("LLM returned None for chat; using empty answer.")
        return {"answer": "", "citedChunkIds": []}

    text = _strip_fences(str(raw))
    if not text.strip():
        logger.warning("LLM returned empty response for chat; using empty answer.")
        return {"answer": "", "citedChunkIds": []}

    # 1) Strict parse
    try:
        data = json.loads(text)
        if isinstance(data, dict):
            logger.info("LLM returned valid JSON for chat.")
            return _normalize_chat_object(data, raw_fallback=text)
    except json.JSONDecodeError:
        pass

    # 2) Extract JSON object substring if present (e.g. surrounding prose)
    s = text.strip()
    start = s.find("{")
    end = s.rfind("}")
    if start != -1 and end > start:
        candidate = s[start : end + 1]
        try:
            data = json.loads(candidate)
            if isinstance(data, dict):
                logger.warning(
                    "Extracted JSON object from LLM chat response with surrounding text."
                )
                return _normalize_chat_object(data, raw_fallback=text)
        except json.JSONDecodeError:
            pass

    # 3) Fallback: use raw text as answer
    logger.warning("JSON parse failed for LLM chat response — using fallback text mode.")
    # Truncate in debug to avoid huge logs / leaking large responses.
    debug_preview = s[:500] + ("…" if len(s) > 500 else "")
    logger.debug("Raw LLM chat response (truncated): %s", debug_preview)
    return {"answer": s, "citedChunkIds": []}
