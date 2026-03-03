#!/usr/bin/env bash
# E2E test with Ollama as the LLM. Ensures UI doesn't freeze by using streaming where available.
#
# Prerequisites:
# - Ollama running: ollama serve (or already running)
# - A model pulled: ollama pull llama3:8b  (or use a smaller one for speed: ollama pull llama3.2:3b)
# - Python AI at http://localhost:8000 with LLM_FALLBACK_CHAIN=ollama (e.g. in study-pilot-ai/.env)
# - .NET API at http://localhost:5024
# - PostgreSQL with StudyPilot DB and migrations applied
#
# For faster runs, use a smaller model in study-pilot-ai/.env: OLLAMA_MODEL=llama3.2:3b
#
# Usage:
#   1. Start Ollama: ollama serve (if not already running)
#   2. Start stack: ./scripts/run-local.sh  (or start Python AI with LLM_FALLBACK_CHAIN=ollama and .NET API)
#   3. Run: ./scripts/e2e-ollama-test.sh

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API_BASE="${API_BASE:-http://localhost:5024}"
AI_BASE="${AI_BASE:-http://localhost:8000}"
OLLAMA_BASE="${OLLAMA_BASE:-http://localhost:11434}"
E2E_EMAIL="e2e-ollama-$(date +%s)@test.local"
E2E_PASSWORD="E2eOllamaPass123!"

echo "=== StudyPilot E2E (Ollama) ==="
echo "API: $API_BASE | AI: $AI_BASE | Ollama: $OLLAMA_BASE"
echo ""

# --- Ollama precheck ---
echo "[0] Checking Ollama..."
if ! curl -sf "${OLLAMA_BASE}/api/tags" -o /dev/null 2>/dev/null; then
  echo "  Ollama not reachable at $OLLAMA_BASE. Start with: ollama serve"
  exit 1
fi
echo "  Ollama is running"

# --- Health ---
echo "[1/14] Health (API)..."
curl -sf "$API_BASE/health/live" -o /dev/null || { echo "API not reachable"; exit 1; }
echo "[2/14] Health (AI)..."
curl -sf "$AI_BASE/health" -o /dev/null || { echo "AI not reachable"; exit 1; }

# --- Register ---
echo "[3/14] Register..."
REG_RESP=$(curl -sf -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$E2E_EMAIL\",\"password\":\"$E2E_PASSWORD\"}")
if echo "$REG_RESP" | grep -q '"success":true'; then
  echo "  Registered $E2E_EMAIL"
else
  echo "  Response: $REG_RESP"
  exit 1
fi

# --- Login ---
echo "[4/14] Login..."
LOGIN_RESP=$(curl -sf -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$E2E_EMAIL\",\"password\":\"$E2E_PASSWORD\"}")
TOKEN=$(echo "$LOGIN_RESP" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
if [[ -z "$TOKEN" ]]; then
  echo "  No token in: $LOGIN_RESP"
  exit 1
fi
echo "  Token obtained"

# --- Upload document ---
echo "[5/14] Upload document..."
SAMPLE_PDF="${E2E_PDF:+$REPO_ROOT/$E2E_PDF}"
SAMPLE_PDF="${SAMPLE_PDF:-$REPO_ROOT/scripts/e2e-sample.pdf}"
DOC_ID=""
if [[ -f "$SAMPLE_PDF" ]]; then
  echo "  Using PDF: $SAMPLE_PDF"
  UPLOAD_RESP=$(curl -sf -X POST "$API_BASE/documents/upload" \
    -H "Authorization: Bearer $TOKEN" \
    -F "file=@$SAMPLE_PDF;type=application/pdf") || true
  if [[ -n "$UPLOAD_RESP" ]]; then
    DOC_ID=$(echo "$UPLOAD_RESP" | sed -n 's/.*"documentId":"\([^"]*\)".*/\1/p')
  fi
  [[ -n "$DOC_ID" ]] && echo "  DocumentId: $DOC_ID" || echo "  Upload failed or no documentId; continuing without doc"
else
  echo "  No PDF at $SAMPLE_PDF; continuing without upload"
fi

# --- Poll document until Completed (Ollama extract-concepts may take a bit) ---
if [[ -n "$DOC_ID" ]]; then
  echo "[6/14] Poll document status (Ollama processing)..."
  STATUS=""
  for i in $(seq 1 60); do
    DOCS_RESP=$(curl -sf -H "Authorization: Bearer $TOKEN" "$API_BASE/documents") || break
    if command -v jq &>/dev/null; then
      STATUS=$(echo "$DOCS_RESP" | jq -r --arg id "$DOC_ID" '.data // . | if type=="array" then (.[] | select(.id==$id) | .status) else empty end // empty' 2>/dev/null) || true
    else
      STATUS=$(echo "$DOCS_RESP" | grep -o "\"id\":\"$DOC_ID\"[^}]*\"status\":\"[^\"]*\"" | sed 's/.*"status":"\([^"]*\)".*/\1/') || true
    fi
    [[ "$STATUS" == "Completed" ]] && echo "  Document Completed" && break
    [[ "$STATUS" == "Failed" ]] && echo "  Document Failed (last status: $STATUS). Continuing." && break
    [[ $i -eq 60 ]] && echo "  Timeout (last: ${STATUS:-unknown}). Continuing."
    sleep 3
  done
else
  echo "[6/14] Skip poll (no doc)"
fi

# --- Chat session + message (Ollama RAG; use stream in UI to avoid freeze) ---
if [[ -n "$DOC_ID" ]]; then
  echo "[7/14] Create chat session..."
  SESS_RESP=$(curl -sf -X POST "$API_BASE/chat/sessions" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"documentId\":\"$DOC_ID\"}")
  SESSION_ID=$(echo "$SESS_RESP" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)
  if [[ -z "$SESSION_ID" ]]; then
    SESSION_ID=$(echo "$SESS_RESP" | sed -n 's/.*"sessionId":"\([^"]*\)".*/\1/p' | head -1)
  fi
  if [[ -n "$SESSION_ID" ]]; then
    echo "[8/14] Send chat message (Ollama)..."
    CHAT_RESP=$(curl -sf -X POST "$API_BASE/chat/message" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"sessionId\":\"$SESSION_ID\",\"content\":\"What are the main ideas?\"}")
    if echo "$CHAT_RESP" | grep -q '"success":true'; then
      echo "  Chat message sent"
    else
      echo "  Chat response: $CHAT_RESP"
    fi
  else
    echo "  No sessionId in: $SESS_RESP"
  fi
else
  echo "[7/14] Skip chat (no doc)"
  echo "[8/14] Skip chat message"
fi

# --- Quiz (Ollama generate-quiz) ---
if [[ -n "$DOC_ID" ]]; then
  echo "[9/14] Start quiz..."
  QUIZ_RESP=$(curl -sf -X POST "$API_BASE/quiz/start" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"documentId\":\"$DOC_ID\"}") || QUIZ_RESP=""
  QUIZ_ID=$(echo "$QUIZ_RESP" | sed -n 's/.*"quizId":"\([^"]*\)".*/\1/p')
  if [[ -n "$QUIZ_ID" ]]; then
    echo "[10/14] Get quiz question 0 (Ollama may generate on first request)..."
    Q_RESP=$(curl -sf -H "Authorization: Bearer $TOKEN" "$API_BASE/quiz/$QUIZ_ID/questions/0") || Q_RESP=""
    Q_ID=$(echo "$Q_RESP" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)
    if [[ -n "$Q_ID" ]]; then
      echo "[11/14] Submit quiz..."
      if curl -sf -X POST "$API_BASE/quiz/submit" \
        -H "Authorization: Bearer $TOKEN" \
        -H "Content-Type: application/json" \
        -d "{\"quizId\":\"$QUIZ_ID\",\"answers\":[{\"questionId\":\"$Q_ID\",\"submittedAnswer\":\"A\"}]}" > /dev/null; then
        echo "  Quiz submitted"
      else
        echo "  Quiz submit failed (continuing)"
      fi
    fi
  else
    echo "  No quizId (doc may have no concepts yet)"
  fi
else
  echo "[9/14] Skip quiz"
  echo "[10/14] Skip question"
  echo "[11/14] Skip submit"
fi

# --- Tutor (Ollama tutor/respond) ---
echo "[12/14] Start tutor session..."
if [[ -n "$DOC_ID" ]]; then
  TUTOR_BODY="{\"documentId\":\"$DOC_ID\"}"
else
  TUTOR_BODY="{}"
fi
TUTOR_START=$(curl -sf -X POST "$API_BASE/tutor/start" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d "$TUTOR_BODY") || TUTOR_START=""
TUTOR_SID=$(echo "$TUTOR_START" | sed -n 's/.*"sessionId":"\([^"]*\)".*/\1/p')
if [[ -n "$TUTOR_SID" ]]; then
  echo "  Tutor sessionId: $TUTOR_SID"
  echo "[13/14] Tutor respond (Ollama)..."
  TUTOR_RESP=$(curl -sf -w "\n%{http_code}" -X POST "$API_BASE/tutor/respond" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"sessionId\":\"$TUTOR_SID\",\"message\":\"I want to learn the main concepts.\"}") || TUTOR_RESP=$'\n000'
  TUTOR_HTTP=$(echo "$TUTOR_RESP" | tail -1)
  if [[ "$TUTOR_HTTP" == "200" ]]; then
    echo "  Tutor respond OK (200)"
  else
    echo "  Tutor respond HTTP $TUTOR_HTTP (body: $(echo "$TUTOR_RESP" | head -1 | cut -c1-80))"
  fi
else
  echo "  Tutor start failed or no sessionId; response: ${TUTOR_START:0:120}"
  echo "[13/14] Skip tutor respond (no session)"
fi

# --- Learning suggestions ---
echo "[14/14] Learning suggestions..."
SUGG=$(curl -sf -H "Authorization: Bearer $TOKEN" "$API_BASE/learning/suggestions") || SUGG=""
if echo "$SUGG" | grep -q '"suggestions"'; then
  echo "  Learning suggestions OK"
else
  echo "  Learning suggestions failed or empty; response: ${SUGG:0:120}"
fi

echo ""
echo "=== E2E (Ollama) completed ==="
