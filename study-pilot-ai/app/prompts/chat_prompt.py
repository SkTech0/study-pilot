from __future__ import annotations


def get_chat_prompt(system: str, question: str, context: list[dict]) -> str:
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

    return (
        "You are StudyPilot Chat.\n"
        f"SYSTEM INSTRUCTION:\n{sys}\n\n"
        "CONTEXT CHUNKS (use these as the ONLY source of truth):\n"
        f"{context_block}\n\n"
        f"QUESTION:\n{q}\n\n"
        "Return ONLY valid JSON with exactly these keys:\n"
        "- answer: string\n"
        "- citedChunkIds: array of chunkId strings you used\n"
        "If the answer is not in the context, answer must say you don't know and citedChunkIds must be empty.\n"
    )
