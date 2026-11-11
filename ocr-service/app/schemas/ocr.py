from typing import Any, Literal

from pydantic import BaseModel, Field


class ImageQuality(BaseModel):
    width: int = Field(gt=0)
    height: int = Field(gt=0)
    blur_score: float
    brightness_mean: float = 0
    contrast_stddev: float = 0


class OCRLine(BaseModel):
    text: str
    confidence: float = Field(ge=0, le=1)
    box: list[list[float]] | None = None


class ConfiguredField(BaseModel):
    field_name: str
    display_name: str
    aliases: list[str] = Field(default_factory=list)
    confidence_threshold: float | None = Field(default=None, ge=0, le=1)


class FieldCandidate(BaseModel):
    field_name: str
    value: str | None = None
    confidence: float = Field(ge=0, le=1)
    box: list[list[float]] | None = None
    status: Literal["DETECTED", "AMBIGUOUS", "NOT_FOUND"]
    reason: str
    debug: dict[str, Any] | None = None


class OCRArtifactReferences(BaseModel):
    original_image: str
    cleaned_image: str
    raw_ocr_text: str
    parsed_json: str


class BarcodeResult(BaseModel):
    format: str
    value: str
    box: list[list[float]] | None = None


class OCRResponse(BaseModel):
    status: Literal["PROCESSING", "REVIEW_REQUIRED"]
    engine: str
    engine_version: str
    raw_text: str
    ocr_confidence: float = Field(ge=0, le=1)
    image_quality: ImageQuality
    lines: list[OCRLine]
    message: str
    field_candidates: list[FieldCandidate] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
    processing_duration_ms: int = Field(ge=0)
    job_id: str | None = None
    artifacts: OCRArtifactReferences | None = None


class BarcodeResponse(BaseModel):
    barcodes: list[BarcodeResult]
    warnings: list[str] = Field(default_factory=list)
    processing_duration_ms: int = Field(ge=0)
