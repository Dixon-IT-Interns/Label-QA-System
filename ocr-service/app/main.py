import json
import logging
from datetime import datetime, timezone

from fastapi import FastAPI

from app.api.routes import router


class JsonLogFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        return json.dumps(
            {
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "level": record.levelname,
                "logger": record.name,
                "message": record.getMessage(),
            }
        )


handler = logging.StreamHandler()
handler.setFormatter(JsonLogFormatter())
logging.basicConfig(level=logging.INFO, handlers=[handler], force=True)

app = FastAPI(
    title="Dixon Label Verification OCR",
    description="Image validation and OCR extraction only; business decisions remain in the ASP.NET API.",
    version="1.0.0",
)
app.include_router(router)
