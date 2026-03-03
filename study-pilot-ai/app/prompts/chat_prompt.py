from __future__ import annotations


def _style_instruction(style: str | None) -> str:
    if not style:
        return ""
    s = (style or "").strip().lower()
    if s == "beginner":
        return (
            "\nEXPLANATION STYLE: Beginner. Use simple language, step-by-step explanations, "
            "and analogies. Avoid jargon; define any technical terms if needed.\n"
        )
    if s == "intermediate":
        return (
            "\nEXPLANATION STYLE: Intermediate. Use clear, structured explanations. "
            "Some technical terms are fine; briefly clarify when helpful.\n"
        )
    if s == "advanced":
        return (
            "\nEXPLANATION STYLE: Advanced. Be concise and technical. Minimal explanation; "
            "assume familiarity with the domain.\n"
        )
    return ""


def get_chat_prompt(
    system: str, question: str, context: list[dict], explanation_style: str | None = None
) -> str:
    """
    Deterministic JSON output contract:
    {
      "answer": "...",
      "citedChunkIds": ["<uuid>", "..."]
    }
    """
    ctx_lines: list[str] = []
    for item in context or []:
        if not isinstance(item, dict):
            continue
        chunk_id = str(item.get("chunkId") or item.get("chunk_id") or "").strip()
        doc_id = str(item.get("documentId") or item.get("document_id") or "").strip()
        text = str(item.get("text") or "").strip()
        if not chunk_id or not text:
            continue
        ctx_lines.append(f"[chunkId={chunk_id} docId={doc_id}] {text}")

    context_block = "\n".join(ctx_lines) if ctx_lines else "(no context provided)"

    sys = (system or "").strip()
    q = (question or "").strip()
    style_inst = _style_instruction(explanation_style)

    return (
        "You are StudyPilot Chat.\n"
        f"SYSTEM INSTRUCTION:\n{sys}\n"
        f"{style_inst}\n"
        "CONTEXT CHUNKS (use these as the ONLY source of truth):\n"
        f"{context_block}\n\n"
        f"QUESTION:\n{q}\n\n"
        "Return ONLY valid JSON with exactly these keys:\n"
        "- answer: string\n"
        "- citedChunkIds: array of chunkId strings you used\n"
        "If the answer is not in the context, answer must say you don't know and citedChunkIds must be empty.\n"
    )
