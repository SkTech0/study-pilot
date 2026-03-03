#!/usr/bin/env bash
# E2E test with mock AI (AI_MODE=mock). Requires:
# - AI service at http://localhost:8000 (run with AI_MODE=mock)
# - .NET API at http://localhost:5024
# - PostgreSQL with StudyPilot DB and migrations applied
#
# Usage: from repo root, start stack with ./scripts/run-local.sh (uses AI_MODE=mock),
# then in another terminal: ./scripts/e2e-mock-test.sh

set -e

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
API_BASE="${API_BASE:-http://localhost:5024}"
AI_BASE="${AI_BASE:-http://localhost:8000}"
E2E_EMAIL="e2e-mock-$(date +%s)@test.local"
E2E_PASSWORD="E2eMockPass123!"

echo "=== StudyPilot E2E (Mock AI) ==="
echo "API: $API_BASE | AI: $AI_BASE"
echo "Tip: In the Python (AI) terminal you should see POST /extract-concepts and POST /embeddings when document processing runs."
echo ""

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
# Use E2E_PDF env to override (e.g. E2E_PDF="ER Modelling.pdf" ./scripts/e2e-mock-test.sh)
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

# --- Poll document until Completed (mock processing is fast) ---
if [[ -n "$DOC_ID" ]]; then
  echo "[6/14] Poll document status..."
  STATUS=""
  for i in $(seq 1 24); do
    DOCS_RESP=$(curl -sf -H "Authorization: Bearer $TOKEN" "$API_BASE/documents") || break
    if command -v jq &>/dev/null; then
      STATUS=$(echo "$DOCS_RESP" | jq -r --arg id "$DOC_ID" '.data // . | if type=="array" then (.[] | select(.id==$id) | .status) else empty end // empty' 2>/dev/null) || true
    else
      STATUS=$(echo "$DOCS_RESP" | grep -o "\"id\":\"$DOC_ID\"[^}]*\"status\":\"[^\"]*\"" | sed 's/.*"status":"\([^"]*\)".*/\1/') || true
    fi
    [[ "$STATUS" == "Completed" ]] && echo "  Document Completed" && break
    [[ $i -eq 24 ]] && echo "  Timeout (last: ${STATUS:-unknown}). Continuing."
    sleep 2
  done
else
  echo "[6/14] Skip poll (no doc)"
fi

# --- Chat session + message (requires document) ---
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
    echo "[8/14] Send chat message..."
    CHAT_RESP=$(curl -sf -X POST "$API_BASE/chat/message" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "{\"sessionId\":\"$SESSION_ID\",\"content\":\"What are variables?\"}")
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

# --- Quiz (requires document with concepts) ---
if [[ -n "$DOC_ID" ]]; then
  echo "[9/14] Start quiz..."
  QUIZ_RESP=$(curl -sf -X POST "$API_BASE/quiz/start" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"documentId\":\"$DOC_ID\"}") || QUIZ_RESP=""
  QUIZ_ID=$(echo "$QUIZ_RESP" | sed -n 's/.*"quizId":"\([^"]*\)".*/\1/p')
  if [[ -n "$QUIZ_ID" ]]; then
    echo "[10/14] Get quiz question 0..."
    Q_RESP=$(curl -sf -H "Authorization: Bearer $TOKEN" "$API_BASE/quiz/$QUIZ_ID/questions/0") || Q_RESP=""
    Q_ID=$(echo "$Q_RESP" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' | head -1)
    if [[ -n "$Q_ID" ]]; then
      echo "[11/14] Submit quiz..."
      if curl -sf -X POST "$API_BASE/quiz/submit" \
        -H "Authorization: Bearer $TOKEN" \
        -H "Content-Type: application/json" \
        -d "{\"quizId\":\"$QUIZ_ID\",\"answers\":[{\"questionId\":\"$Q_ID\",\"submittedAnswer\":\"Storage\"}]}" > /dev/null; then
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

# --- Tutor (steps 12–13: start session + respond; Python POST /tutor/respond) ---
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
  echo "[13/14] Tutor respond (calls Python POST /tutor/respond)..."
  TUTOR_RESP=$(curl -sf -w "\n%{http_code}" -X POST "$API_BASE/tutor/respond" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"sessionId\":\"$TUTOR_SID\",\"message\":\"I want to learn variables.\"}") || TUTOR_RESP=$'\n000'
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

# --- Learning suggestions (step 14) ---
echo "[14/14] Learning suggestions..."
SUGG=$(curl -sf -H "Authorization: Bearer $TOKEN" "$API_BASE/learning/suggestions") || SUGG=""
if echo "$SUGG" | grep -q '"suggestions"'; then
  echo "  Learning suggestions OK"
else
  echo "  Learning suggestions failed or empty; response: ${SUGG:0:120}"
fi

echo ""
echo "=== E2E (Mock AI) completed ==="
