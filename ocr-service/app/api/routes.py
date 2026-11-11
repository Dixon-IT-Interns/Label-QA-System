import logging
import json
from pathlib import PurePosixPath, PureWindowsPath

from fastapi import APIRouter, File, Form, HTTPException, UploadFile
from pydantic import ValidationError
from starlette.concurrency import run_in_threadpool

from app.config import ALLOWED_IMAGE_TYPES, MAX_UPLOAD_BYTES, OCR_ARTIFACTS_DIR
from app.image_processing.quality import ImageQualityError
from app.ocr.factory import create_ocr_provider
from app.ocr.provider import OCRUnavailableError
from app.schemas.ocr import BarcodeResponse, ConfiguredField, OCRResponse
from app.services.artifact_storage import OCRArtifactStore
from app.services.ocr_service import OCRService

logger = logging.getLogger(__name__)
router = APIRouter()
ocr_service = OCRService(
    create_ocr_provider(),
    OCRArtifactStore(OCR_ARTIFACTS_DIR),
)


@router.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok", "service": "label-ocr"}


@router.post("/ocr/extract", response_model=OCRResponse)
async def extract_label(
    file: UploadFile = File(...),
    field_config: str | None = Form(default=None),
    minimum_confidence: float | None = Form(default=None),
    label_type: str | None = Form(default=None),
) -> OCRResponse:
    if file.content_type not in ALLOWED_IMAGE_TYPES:
        raise HTTPException(status_code=415, detail="Only JPEG, PNG, and WebP images are supported.")

    filename = file.filename or ""
    suffix = PurePosixPath(filename).suffix.lower() or PureWindowsPath(filename).suffix.lower()
    if suffix not in ALLOWED_IMAGE_TYPES[file.content_type]:
        raise HTTPException(status_code=415, detail="The file extension does not match the image content type.")

    image_bytes = await file.read(MAX_UPLOAD_BYTES + 1)
    if not image_bytes:
        raise HTTPException(status_code=400, detail="The uploaded image is empty.")
    if len(image_bytes) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"Image exceeds the {MAX_UPLOAD_BYTES}-byte upload limit.")
    if minimum_confidence is not None and not 0 <= minimum_confidence <= 1:
        raise HTTPException(status_code=422, detail="minimum_confidence must be between 0 and 1.")

    try:
        fields = [ConfiguredField.model_validate(item) for item in json.loads(field_config)] if field_config else []
        return await run_in_threadpool(
            ocr_service.inspect,
            image_bytes,
            fields,
            minimum_confidence,
            suffix,
            label_type,
        )
    except ImageQualityError as error:
        raise HTTPException(
            status_code=422,
            detail={"message": str(error), "image_quality": error.quality.model_dump()},
        ) from error
    except ValidationError as error:
        raise HTTPException(status_code=422, detail=error.errors()) from error
    except ValueError as error:
        raise HTTPException(status_code=422, detail=str(error)) from error
    except OCRUnavailableError as error:
        raise HTTPException(status_code=503, detail=str(error)) from error
    except Exception:
        logger.exception("Unexpected OCR extraction failure")
        raise HTTPException(status_code=500, detail="OCR extraction failed; see service logs.") from None


@router.post("/ocr/barcode", response_model=BarcodeResponse)
async def extract_barcodes(file: UploadFile = File(...)) -> BarcodeResponse:
    if file.content_type not in ALLOWED_IMAGE_TYPES:
        raise HTTPException(status_code=415, detail="Only JPEG, PNG, and WebP images are supported.")
    filename = file.filename or ""
    suffix = PurePosixPath(filename).suffix.lower() or PureWindowsPath(filename).suffix.lower()
    if suffix not in ALLOWED_IMAGE_TYPES[file.content_type]:
        raise HTTPException(status_code=415, detail="The file extension does not match the image content type.")
    image_bytes = await file.read(MAX_UPLOAD_BYTES + 1)
    if not image_bytes:
        raise HTTPException(status_code=400, detail="The uploaded image is empty.")
    if len(image_bytes) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail=f"Image exceeds the {MAX_UPLOAD_BYTES}-byte upload limit.")
    try:
        return await run_in_threadpool(ocr_service.decode_barcodes, image_bytes)
    except ImageQualityError as error:
        raise HTTPException(status_code=422, detail=str(error)) from error
    except ValueError as error:
        raise HTTPException(status_code=422, detail=str(error)) from error
    except ImportError as error:
        logger.exception("Barcode decoding dependency is unavailable")
        raise HTTPException(status_code=503, detail="Barcode decoding is unavailable.") from error
    except Exception:
        logger.exception("Unexpected barcode decoding failure")
        raise HTTPException(status_code=500, detail="Barcode decoding failed; see service logs.") from None
