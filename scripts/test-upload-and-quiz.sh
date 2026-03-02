#!/usr/bin/env bash
# End-to-end test: Upload document (frontend -> .NET API -> storage + job),
# then Start quiz (.NET API -> Python AI). Use curl against .NET API and optionally Python API.
#
# Prerequisites:
#   - .NET API running (e.g. http://localhost:5024 or 5000)
#   - Python AI running (http://localhost:8000)
#   - Optional: jq for parsing JSON
#   - A small PDF file for upload (e.g. create one or use any valid PDF)
#
# Usage:
#   export TEST_PDF_PATH="/path/to/test.pdf"   # optional; if unset, upload is skipped
#   ./scripts/test-upload-and-quiz.sh

set -e

# Config (match proxy.conf.json and RUN.md)
API_BASE="${API_BASE:-http://localhost:5024}"
AI_BASE="${AI_BASE:-http://localhost:8000}"
TEST_USER="${TEST_USER:-test@example.com}"
TEST_PASS="${TEST_PASS:-TestPass123!}"
TEST_PDF_PATH="${TEST_PDF_PATH:-}"

echo "=== StudyPilot E2E curl tests ==="
echo "API (backend): $API_BASE"
echo "AI (Python):   $AI_BASE"
echo ""

# --- 1) Health (no auth) ---
echo "1) Backend health..."
curl -s -o /dev/null -w "%{http_code}" "$API_BASE/health/live" | grep -q 200 && echo "   OK" || { echo "   FAIL (is the API running?)"; exit 1; }

echo "2) Python AI health..."
curl -s "$AI_BASE/health" | head -1
echo ""

# --- 3) Register then Login ---
echo "3) Register / Login..."
LOGIN_RESP=$(curl -s -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$TEST_USER\",\"password\":\"$TEST_PASS\"}" 2>/dev/null || true)
if echo "$LOGIN_RESP" | grep -q '"success":true'; then
  echo "   Registered and logged in."
else
  LOGIN_RESP=$(curl -s -X POST "$API_BASE/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$TEST_USER\",\"password\":\"$TEST_PASS\"}")
fi
if ! echo "$LOGIN_RESP" | grep -q '"success":true'; then
  echo "   Login failed. Response: $LOGIN_RESP"
  exit 1
fi
if command -v jq &>/dev/null; then
  TOKEN=$(echo "$LOGIN_RESP" | jq -r '.data.accessToken')
else
  TOKEN=$(echo "$LOGIN_RESP" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')
fi
if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
  echo "   Could not extract accessToken."
  exit 1
fi
echo "   Token obtained."
echo ""

# --- 4) Upload document (optional if TEST_PDF_PATH set) ---
DOC_ID=""
if [ -n "$TEST_PDF_PATH" ] && [ -f "$TEST_PDF_PATH" ]; then
  echo "4) Upload document..."
  UPLOAD_RESP=$(curl -s -X POST "$API_BASE/documents/upload" \
    -H "Authorization: Bearer $TOKEN" \
    -F "file=@$TEST_PDF_PATH")
  if echo "$UPLOAD_RESP" | grep -q '"success":true'; then
    if command -v jq &>/dev/null; then
      DOC_ID=$(echo "$UPLOAD_RESP" | jq -r '.data.documentId')
    else
      DOC_ID=$(echo "$UPLOAD_RESP" | sed -n 's/.*"documentId":"\([^"]*\)".*/\1/p')
    fi
    echo "   Upload OK. documentId=$DOC_ID (processing may still be running)."
  else
    echo "   Upload failed: $UPLOAD_RESP"
    exit 1
  fi
else
  echo "4) Skip upload (set TEST_PDF_PATH to a PDF file to test upload)."
  # To test start-quiz without upload, set DOC_ID_FOR_QUIZ to an existing processed document id:
  [ -n "${DOC_ID_FOR_QUIZ:-}" ] && DOC_ID="$DOC_ID_FOR_QUIZ" && echo "   Using DOC_ID_FOR_QUIZ=$DOC_ID for start-quiz."
fi
echo ""

# --- 5) Start quiz (requires a document that has been processed and has concepts) ---
if [ -n "$DOC_ID" ]; then
  echo "5) Start quiz for documentId=$DOC_ID..."
  QUIZ_RESP=$(curl -s -X POST "$API_BASE/quiz/start" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d "{\"documentId\":\"$DOC_ID\"}")
  if echo "$QUIZ_RESP" | grep -q '"success":true'; then
    echo "   Start quiz OK."
    if command -v jq &>/dev/null; then
      echo "$QUIZ_RESP" | jq '.data'
    else
      echo "$QUIZ_RESP"
    fi
  else
    echo "   Start quiz response (may fail if document not yet processed): $QUIZ_RESP"
  fi
else
  echo "5) Skip start-quiz (no documentId). Upload a PDF and set DOC_ID_FOR_QUIZ or re-run with TEST_PDF_PATH."
fi
echo ""

# --- 6) Optional: call Python AI directly (no auth) ---
echo "6) Python AI - extract-concepts (sample text)..."
EXTRACT_RESP=$(curl -s -X POST "$AI_BASE/extract-concepts" \
  -H "Content-Type: application/json" \
  -d '{"documentId":"00000000-0000-0000-0000-000000000001","text":"Machine learning is a subset of artificial intelligence. Neural networks are used in deep learning."}')
echo "$EXTRACT_RESP" | head -c 200
echo ""
echo ""

echo "7) Python AI - generate-quiz (sample concepts)..."
QUIZ_AI_RESP=$(curl -s -X POST "$AI_BASE/generate-quiz" \
  -H "Content-Type: application/json" \
  -d '{"documentId":"00000000-0000-0000-0000-000000000001","concepts":[{"name":"Machine learning"},{"name":"Neural networks"}],"questionCount":2}')
echo "$QUIZ_AI_RESP" | head -c 300
echo ""
echo ""

echo "=== Done ==="
