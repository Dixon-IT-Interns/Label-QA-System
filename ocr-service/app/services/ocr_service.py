import logging
import math
import re
import time

import cv2
import numpy as np

from app.config import MIN_BLUR_SCORE, MIN_OCR_CONFIDENCE, OCR_PREPROCESSING, TESSERACT_CONFIG

from app.image_processing.annotations import has_blue_annotation_like_marks, prepare_for_ocr
from app.image_processing.annotations import remove_blue_annotation_pixels
from app.image_processing.config_label import prepare_config_label
from app.image_processing.quality import decode_and_check_image
from app.ocr.provider import OCRProvider
from app.services.artifact_storage import OCRArtifactStore
from app.services.field_matching import match_configured_fields
from app.services.queue_number import extract_queue_number
from app.schemas.ocr import (
    BarcodeResponse,
    BarcodeResult,
    ConfiguredField,
    FieldCandidate,
    OCRLine,
    OCRResponse,
)

logger = logging.getLogger(__name__)


class OCRService:
    def __init__(
        self,
        provider: OCRProvider,
        artifact_store: OCRArtifactStore | None = None,
    ) -> None:
        self._provider = provider
        self._artifact_store = artifact_store

    def inspect(
        self,
        image_bytes: bytes,
        configured_fields: list[ConfiguredField] | None = None,
        minimum_confidence: float | None = None,
        original_extension: str = ".img",
    ) -> OCRResponse:
        started = time.perf_counter()
        logger.info("OCR image received; byte_count=%d", len(image_bytes))
        image, quality = decode_and_check_image(image_bytes)
        logger.info(
            "Image preprocessing started; width=%d height=%d method=%s",
            quality.width,
            quality.height,
            OCR_PREPROCESSING,
        )
        warnings = []
        if quality.blur_score < MIN_BLUR_SCORE:
            warnings.append("Image sharpness is below the configured quality threshold.")
        if quality.brightness_mean < 25:
            warnings.append("Image lighting is unusually dark; check the capture before accepting OCR.")
        if quality.contrast_stddev < 12:
            warnings.append("Image contrast is too low for reliable text extraction.")
        blue_annotation_detected = has_blue_annotation_like_marks(image)
        if blue_annotation_detected:
            warnings.append(
                "Blue annotation-like pixels were masked from OCR preprocessing; the original image was preserved."
            )
        active_fields = configured_fields or []
        is_config_pair = _is_config_field_pair(active_fields)
        rectification: dict[str, object] | None = None
        if is_config_pair:
            processing_image, rectification = prepare_config_label(image_bytes, image)
            cleaned_image = remove_blue_annotation_pixels(processing_image)
        else:
            cleaned_image = prepare_for_ocr(image, quality, OCR_PREPROCESSING)
        logger.info("Image preprocessing completed")
        if is_config_pair:
            active_fields = [
                field for field in active_fields
                if _normalized_field_name(field.field_name) in {"modelno", "queueno"}
            ]
            logger.info("Targeted CONFIG OCR started; engine=%s", self._provider.name)
            field_candidates, lines, raw_text, engine_version = self._extract_config_fields(
                cleaned_image,
                active_fields,
                rectification or {},
            )
        else:
            logger.info("OCR started; engine=%s", self._provider.name)
            ocr_result = self._provider.extract(cleaned_image)
            lines = ocr_result.lines
            raw_text = ocr_result.raw_text
            engine_version = ocr_result.version
            field_candidates = []

        readable_lines = [line for line in lines if line.text.strip()]
        ocr_confidence = (
            sum(candidate.confidence for candidate in field_candidates) / len(field_candidates)
            if is_config_pair and field_candidates
            else sum(line.confidence for line in readable_lines) / len(readable_lines)
            if readable_lines
            else 0
        )
        logger.info(
            "OCR completed; engine=%s line_count=%d confidence=%.3f",
            self._provider.name,
            len(readable_lines),
            ocr_confidence,
        )
        confidence_threshold = MIN_OCR_CONFIDENCE if minimum_confidence is None else minimum_confidence
        thresholds = {
            field.field_name: field.confidence_threshold
            if field.confidence_threshold is not None
            else confidence_threshold
            for field in active_fields
        }
        requires_review = (
            any(
                candidate.status != "DETECTED"
                or candidate.confidence < thresholds.get(candidate.field_name, confidence_threshold)
                for candidate in field_candidates
            )
            if is_config_pair
            else (
                not readable_lines
                or quality.blur_score < MIN_BLUR_SCORE
                or quality.brightness_mean < 25
                or quality.contrast_stddev < 12
            )
        )
        logger.info("Configured field extraction started; field_count=%d", len(active_fields))
        if not is_config_pair:
            field_candidates = match_configured_fields(active_fields, readable_lines)
        candidates_by_name = {candidate.field_name: candidate for candidate in field_candidates}
        retry_fields = [
            field for field in active_fields
            if not is_config_pair
            and (
                (candidate := candidates_by_name[field.field_name]).status == "NOT_FOUND"
                or candidate.status == "DETECTED"
                and candidate.confidence < thresholds[field.field_name]
            )
        ]
        if retry_fields:
            if is_config_pair:
                retry_candidates, retry_lines, retry_text = self._retry_config_fields(
                    cleaned_image,
                    retry_fields,
                )
                lines.extend(retry_lines)
                raw_text = "\n".join(part for part in (raw_text, retry_text) if part)
                field_candidates = _merge_retry_candidates(field_candidates, retry_candidates, 1.0)
            else:
                maximum_retry_pixels = 10_000_000
                scale = min(
                    3.0,
                    math.sqrt(maximum_retry_pixels / (cleaned_image.shape[0] * cleaned_image.shape[1])),
                )
                if scale >= 1.2:
                    retry_image = cv2.resize(
                        cleaned_image,
                        None,
                        fx=scale,
                        fy=scale,
                        interpolation=cv2.INTER_CUBIC,
                    )
                    retry_result = self._provider.extract(retry_image)
                    retry_candidates = match_configured_fields(retry_fields, retry_result.lines)
                    field_candidates = _merge_retry_candidates(
                        field_candidates,
                        retry_candidates,
                        scale,
                    )
        if any(
            candidate.status == "DETECTED"
            and candidate.confidence < thresholds.get(candidate.field_name, confidence_threshold)
            for candidate in field_candidates
        ):
            warnings.append(
                "A configured field candidate is below the confidence threshold and requires review."
            )

        logger.info(
            "Configured field extraction completed; results=%s",
            [
                {"field": candidate.field_name, "status": candidate.status, "confidence": candidate.confidence}
                for candidate in field_candidates
            ],
        )
        response = OCRResponse(
            status="REVIEW_REQUIRED" if requires_review else "PROCESSING",
            engine=self._provider.name,
            engine_version=engine_version,
            raw_text=raw_text,
            ocr_confidence=ocr_confidence,
            image_quality=quality,
            lines=readable_lines,
            field_candidates=field_candidates,
            warnings=warnings,
            processing_duration_ms=round((time.perf_counter() - started) * 1000),
            message=(
                "OCR evidence requires review. This service does not compare labels or determine inspection PASS."
                if requires_review
                else "OCR extraction completed; the ASP.NET API must perform configured field comparisons."
            ),
        )
        if self._artifact_store is not None:
            response = self._artifact_store.persist(
                image_bytes,
                original_extension,
                cleaned_image,
                response,
            )
        logger.info(
            "OCR request completed; status=%s duration_ms=%d job_id=%s",
            response.status,
            response.processing_duration_ms,
            response.job_id,
        )
        return response

    def _extract_config_fields(
        self,
        image: np.ndarray,
        fields: list[ConfiguredField],
        rectification: dict[str, object],
    ) -> tuple[list[FieldCandidate], list[OCRLine], str, str]:
        candidates = []
        lines = []
        raw_text = []
        versions = []
        height, width = image.shape[:2]
        height, width = image.shape[:2]
        for field in fields:
            kind = _normalized_field_name(field.field_name)
            if kind == "queueno":
                candidate = extract_queue_number(image, self._provider)
                roi = candidate.debug["roi"]
                candidate.debug["rectification"] = rectification
                candidate.box = [
                    [float(roi["x"]), float(roi["y"])],
                    [float(roi["x"] + roi["width"]), float(roi["y"])],
                    [float(roi["x"] + roi["width"]), float(roi["y"] + roi["height"])],
                    [float(roi["x"]), float(roi["y"] + roi["height"])],
                ]
                candidates.append(candidate)
                pass_results = candidate.debug.get("passResults", [])
                for pass_result in pass_results:
                    text = pass_result["text"]
                    if text:
                        if text not in raw_text:
                            raw_text.append(text)
                        if text not in {line.text for line in lines}:
                            lines.append(OCRLine(
                                text=text,
                                confidence=pass_result["confidence"] / 100,
                                box=candidate.box,
                            ))
                continue

            top = int(height * 0.04)
            bottom = max(top + 1, int(height * 0.17))
            left = int(width * 0.08)
            right = max(left + 1, int(width * 0.92))
            roi = image[top:bottom, left:right]
            candidate, model_lines, model_text, version = _extract_model_number(
                field,
                roi,
                self._provider,
                left,
                top,
            )
            candidate.debug["roi"] = {
                "x": left,
                "y": top,
                "width": right - left,
                "height": bottom - top,
            }
            candidate.debug["roiDetected"] = True
            candidate.debug["rectification"] = rectification
            candidates.append(candidate)
            lines.extend(model_lines)
            raw_text.append(model_text)
            versions.append(version)
        unique_lines: list[str] = []
        for part in raw_text:
            for line in part.splitlines():
                if line and line not in unique_lines:
                    unique_lines.append(line)
        return candidates, lines, "\n".join(unique_lines), ", ".join(dict.fromkeys(versions))

    def _retry_config_fields(
        self,
        image: np.ndarray,
        fields: list[ConfiguredField],
    ) -> tuple[list[FieldCandidate], list[OCRLine], str]:
        candidates = []
        lines = []
        raw_text = []
        height, width = image.shape[:2]
        for field in fields:
            kind = _normalized_field_name(field.field_name)
            top_ratio, bottom_ratio = {
                "modelno": (0.04, 0.17),
                "queueno": (0.87, 0.95),
            }[kind]
            top = int(height * top_ratio)
            bottom = max(top + 1, int(height * bottom_ratio))
            left = int(width * 0.08)
            right = max(left + 1, int(width * 0.92))
            roi = image[top:bottom, left:right]
            scale = min(3.0, max(1.0, 1200 / max(roi.shape[:2])))
            retry_image = cv2.resize(
                roi,
                None,
                fx=scale,
                fy=scale,
                interpolation=cv2.INTER_CUBIC,
            )
            result = self._provider.extract(retry_image)
            unscaled_lines = _offset_lines(
                _scale_lines(result.lines, 1 / scale),
                left,
                top,
            )
            candidates.append(_match_config_roi_field(field, unscaled_lines))
            lines.extend(unscaled_lines)
            raw_text.append(result.raw_text)
        return candidates, lines, "\n".join(raw_text)

    @staticmethod
    def decode_barcodes(image_bytes: bytes) -> BarcodeResponse:
        import zxingcpp

        started = time.perf_counter()
        image, _ = decode_and_check_image(image_bytes)
        results = zxingcpp.read_barcodes(image)
        barcodes = []
        for result in results:
            position = getattr(result, "position", None)
            box = None
            if position is not None:
                points = [getattr(position, name, None) for name in ("top_left", "top_right", "bottom_right", "bottom_left")]
                if all(point is not None for point in points):
                    box = [[float(point.x), float(point.y)] for point in points]
            barcodes.append(
                BarcodeResult(format=str(result.format), value=str(result.text), box=box)
            )
        return BarcodeResponse(
            barcodes=barcodes,
            warnings=[] if barcodes else ["No QR code or supported barcode was detected."],
            processing_duration_ms=round((time.perf_counter() - started) * 1000),
        )


def _merge_retry_candidates(
    original: list[FieldCandidate],
    retried: list[FieldCandidate],
    scale: float,
) -> list[FieldCandidate]:
    retried_by_name = {candidate.field_name: candidate for candidate in retried}
    merged = []
    for candidate in original:
        retry = retried_by_name.get(candidate.field_name)
        if retry is None or retry.status == "NOT_FOUND":
            merged.append(candidate)
            continue
        if retry.status == "AMBIGUOUS":
            merged.append(retry)
            continue
        if candidate.status == "NOT_FOUND":
            merged.append(_restore_box_scale(retry, scale))
            continue
        if candidate.status != "DETECTED":
            merged.append(candidate)
            continue
        if _normalize_candidate(candidate.field_name, candidate.value) != _normalize_candidate(
            retry.field_name,
            retry.value,
        ):
            merged.append(candidate.model_copy(update={
                "value": None,
                "box": None,
                "status": "AMBIGUOUS",
                "reason": "The original and enhanced OCR passes produced different values for this field.",
            }))
            continue
        merged.append(
            _restore_box_scale(retry, scale)
            if retry.confidence > candidate.confidence
            else candidate
        )
    return merged


def _normalized_field_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.casefold())


def _is_config_field_pair(fields: list[ConfiguredField]) -> bool:
    names = {_normalized_field_name(field.field_name) for field in fields}
    return {"modelno", "queueno"}.issubset(names)


def _offset_lines(lines: list[OCRLine], x_offset: int, y_offset: int) -> list[OCRLine]:
    return [
        OCRLine(
            text=line.text,
            confidence=line.confidence,
            box=[
                [point[0] + x_offset, point[1] + y_offset]
                for point in line.box
            ] if line.box else None,
        )
        for line in lines
    ]


def _scale_lines(lines: list[OCRLine], scale: float) -> list[OCRLine]:
    return [
        OCRLine(
            text=line.text,
            confidence=line.confidence,
            box=[
                [point[0] * scale, point[1] * scale]
                for point in line.box
            ] if line.box else None,
        )
        for line in lines
    ]


def _extract_model_number(
    field: ConfiguredField,
    roi: np.ndarray,
    provider: OCRProvider,
    x_offset: int,
    y_offset: int,
) -> tuple[FieldCandidate, list[OCRLine], str, str]:
    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY) if roi.ndim == 3 else roi.copy()
    scale = min(4.0, max(1.0, 1200 / max(gray.shape[:2])))
    enlarged = cv2.resize(
        gray,
        None,
        fx=scale,
        fy=scale,
        interpolation=cv2.INTER_CUBIC,
    )
    clahe = cv2.createCLAHE(clipLimit=1.8, tileGridSize=(8, 8)).apply(enlarged)
    denoised = cv2.fastNlMeansDenoising(
        clahe,
        None,
        h=5,
        templateWindowSize=7,
        searchWindowSize=21,
    )
    otsu = cv2.threshold(enlarged, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)[1]
    block_size = min(31, min(enlarged.shape[:2]) if min(enlarged.shape[:2]) % 2 else min(enlarged.shape[:2]) - 1)
    block_size = max(3, block_size)
    adaptive_gaussian = cv2.adaptiveThreshold(
        enlarged,
        255,
        cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        cv2.THRESH_BINARY,
        block_size,
        5,
    )
    adaptive_mean = cv2.adaptiveThreshold(
        enlarged,
        255,
        cv2.ADAPTIVE_THRESH_MEAN_C,
        cv2.THRESH_BINARY,
        block_size,
        5,
    )
    blurred = cv2.GaussianBlur(enlarged, (0, 0), 0.8)
    sharpened = cv2.addWeighted(enlarged, 1.2, blurred, -0.2, 0)
    sharpened_otsu = cv2.threshold(
        sharpened,
        0,
        255,
        cv2.THRESH_BINARY + cv2.THRESH_OTSU,
    )[1]
    variants = {
        "contrast": cv2.convertScaleAbs(enlarged, alpha=1.12, beta=4),
        "clahe_denoise": denoised,
        "otsu": otsu,
        "adaptive_gaussian": adaptive_gaussian,
        "adaptive_mean": adaptive_mean,
        "sharpened_otsu": sharpened_otsu,
    }

    collected: dict[str, dict[str, object]] = {}
    lines: list[OCRLine] = []
    raw_text: list[str] = []
    versions: list[str] = []
    pass_results: list[dict[str, object]] = []
    for variant, processed in variants.items():
        for psm in (7, 13, 6):
            config = re.sub(r"--psm\s+\d+", f"--psm {psm}", TESSERACT_CONFIG)
            if config == TESSERACT_CONFIG and not re.search(r"--psm\s+\d+", config):
                config = f"{config} --psm {psm}".strip()
            result = provider.extract_with_config(processed, config)
            text = result.raw_text.strip()
            if text and text not in raw_text:
                raw_text.append(text)
            versions.append(result.version)
            pass_results.append({
                "variant": variant,
                "psm": psm,
                "text": text,
                "confidence": round(
                    100 * max((line.confidence for line in result.lines), default=0)
                ),
            })
            unscaled_lines = _offset_lines(
                _scale_lines(result.lines, 1 / scale),
                x_offset,
                y_offset,
            )
            lines.extend(unscaled_lines)
            for line in result.lines:
                text = line.text.translate({
                    ord("—"): "-",
                    ord("–"): "-",
                    ord("−"): "-",
                    ord("‑"): "-",
                    ord("‐"): "-",
                }).replace("\ufffd", "")
                if not re.search(r"\b\d{2}-[A-Z]{2}\d{4,}[A-Z0-9]{0,8}\b", text, re.IGNORECASE):
                    continue
                value = re.sub(r"\s+", " ", text.strip()).strip(" |:;")
                key = re.sub(r"\s+", " ", value).casefold()
                entry = collected.setdefault(
                    key,
                    {"value": value, "confidences": [], "passes": [], "boxes": []},
                )
                entry["confidences"].append(line.confidence)
                entry["passes"].append({"variant": variant, "psm": psm})
                if line.box:
                    entry["boxes"].append(
                        [[point[0] / scale + x_offset, point[1] / scale + y_offset] for point in line.box]
                    )

    def candidate_score(entry: dict[str, object]) -> float:
        average = sum(entry["confidences"]) / len(entry["confidences"])
        variants_seen = len({vote["variant"] for vote in entry["passes"]})
        return 0.65 * average + 0.35 * min(1.0, variants_seen / 3)

    ranked = sorted(
        collected.items(),
        key=lambda item: (
            candidate_score(item[1]),
            len({entry["variant"] for entry in item[1]["passes"]}),
            len(item[1]["passes"]),
        ),
        reverse=True,
    )
    candidate_debug = []
    selected_value = None
    selected_confidence = 0.0
    selected_box = None
    if ranked:
        _, selected = ranked[0]
        avg_confidence = sum(selected["confidences"]) / len(selected["confidences"])
        variant_frequency = len({entry["variant"] for entry in selected["passes"]})
        selected_confidence = min(1.0, 0.65 * avg_confidence + 0.35 * min(1.0, variant_frequency / 3))
        top_variant_count = variant_frequency
        runner_up_score = candidate_score(ranked[1][1]) if len(ranked) > 1 else 0
        if top_variant_count >= 2 and candidate_score(selected) > runner_up_score:
            selected_value = str(selected["value"])
            selected_box = max(selected["boxes"], key=lambda box: box[2][1] - box[0][1]) if selected["boxes"] else None
    for _, entry in ranked:
        confidence = sum(entry["confidences"]) / len(entry["confidences"])
        candidate_debug.append(
            {
                "value": entry["value"],
                "confidence": round(100 * confidence),
                "frequency": len(entry["passes"]),
                "variantFrequency": len({item["variant"] for item in entry["passes"]}),
            }
        )

    candidate = FieldCandidate(
        field_name=field.field_name,
        value=selected_value,
        confidence=selected_confidence,
        box=selected_box,
        status="DETECTED" if selected_value else "AMBIGUOUS" if collected else "NOT_FOUND",
        reason=(
            "Model No. was selected from repeated, field-specific OCR candidates."
            if selected_value
            else "Model No. OCR candidates did not agree across preprocessing variants."
            if collected
            else "No model-number pattern was detected in the dedicated top-label region."
        ),
        debug={
            "field": field.display_name,
            "roiDetected": bool(roi.size),
            "preprocessingVariants": len(variants),
            "ocrPasses": len(variants) * 3,
            "candidates": candidate_debug,
            "selectedValue": selected_value,
            "passResults": pass_results,
        },
    )
    return candidate, lines, "\n".join(part for part in raw_text if part), ", ".join(dict.fromkeys(versions))


def _match_config_roi_field(field: ConfiguredField, lines: list[OCRLine]) -> FieldCandidate:
    kind = _normalized_field_name(field.field_name)
    if kind == "modelno":
        return match_configured_fields([field], lines)[0]

    candidate_pattern = re.compile(
        r"(?<![A-Z0-9])(?P<value>[A-Z0-9]{5,}(?:\s*#\s*[A-Z0-9]{2,})?)(?![A-Z0-9])",
        re.IGNORECASE,
    )
    values = {}
    for line in lines:
        for match in candidate_pattern.finditer(line.text):
            value = re.sub(r"\s+", "", match.group("value"))
            if not any(character.isdigit() for character in value):
                continue
            values[value.upper()] = (value, line.confidence, line.box)

    if len(values) == 1:
        value, confidence, box = next(iter(values.values()))
        return FieldCandidate(
            field_name=field.field_name,
            value=value,
            confidence=confidence,
            box=box,
            status="DETECTED",
            reason="A human-readable Queue No. candidate was detected in the below-barcode region.",
        )
    if len(values) > 1:
        return FieldCandidate(
            field_name=field.field_name,
            confidence=min(confidence for _, confidence, _ in values.values()),
            status="AMBIGUOUS",
            reason="Multiple distinct Queue No. candidates were detected below the barcode.",
        )
    return FieldCandidate(
        field_name=field.field_name,
        confidence=0,
        status="NOT_FOUND",
        reason="No human-readable Queue No. candidate was detected below the barcode.",
    )


def _normalize_candidate(field_name: str, value: str | None) -> str:
    if value is None:
        return ""
    cleaned = value.replace("®", "").replace("™", "")
    normalized = cleaned.casefold()
    field_key = re.sub(r"[^a-z0-9]", "", field_name.casefold())
    if field_key == "queueno":
        return "".join(character for character in normalized if not character.isspace())
    if field_key == "modelno":
        model = re.search(r"\b\d{2}-[a-z]{2}\d{4,}[a-z0-9]{0,8}\b", normalized)
        if model is not None:
            return model.group(0)
    if field_key in {"storage", "ssd", "harddrive"}:
        capacity = re.search(r"(\d+(?:\.\d+)?)\s*(gb|tb)\b", normalized)
        if capacity is not None:
            storage_type = (
                "nvme solid state drive"
                if re.search(r"\bnvme\b", normalized)
                else "solid state drive"
                if re.search(r"\b(?:ssd|solid\s+state\s+drive)\b", normalized)
                else "hard disk drive"
                if re.search(r"\b(?:hdd|hard\s+disk(?:\s+drive)?)\b", normalized)
                else "emmc"
                if re.search(r"\bemmc\b", normalized)
                else ""
            )
            if storage_type:
                return f"{capacity.group(1)}{capacity.group(2)} {storage_type}"
    if field_key in {"processor", "cpu"}:
        normalized = re.sub(r"\bprocessor\b", "", normalized)
    if field_key in {"model", "modelnumber", "modelid", "modelno"}:
        model = re.search(r"\b\d{2}-[a-z]{2}\d{4,}[a-z0-9]{0,8}\b", normalized)
        if model is not None:
            return model.group(0)
    return re.sub(r"[^a-z0-9.]", "", normalized)


def _restore_box_scale(candidate: FieldCandidate, scale: float) -> FieldCandidate:
    if candidate.box is None:
        return candidate
    return candidate.model_copy(update={
        "box": [[coordinate / scale for coordinate in point] for point in candidate.box],
    })
