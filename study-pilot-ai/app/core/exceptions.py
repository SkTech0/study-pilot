from fastapi import Request
from fastapi.responses import JSONResponse
import httpx


async def global_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    if isinstance(exc, httpx.HTTPStatusError) and exc.response.status_code == 429:
        retry_after = exc.response.headers.get("retry-after")
        return JSONResponse(
            status_code=429,
            headers={"Retry-After": retry_after} if retry_after else None,
            content={"detail": "Rate limited by Gemini API", "type": "RateLimited"},
        )
    return JSONResponse(
        status_code=500,
        content={"detail": "Internal server error", "type": type(exc).__name__},
    )
