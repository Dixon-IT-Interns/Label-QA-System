import logging
import math
import re
import time
from urllib.parse import unquote

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
        label_type: str | None = None,
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
        normalized_label_type = (label_type or "").strip().casefold()
        if normalized_label_type and normalized_label_type not in {"co", "config"}:
            raise ValueError("label_type must be either 'CO' or 'CONFIG'.")
        is_co_label = (
            normalized_label_type == "co"
            if normalized_label_type
            else _is_co_field_set(active_fields)
        )
        has_config_pair = _is_config_field_pair(active_fields)
        if is_co_label and has_config_pair:
            raise ValueError("A label cannot be configured as both CO and CONFIG.")
        is_config_pair = has_config_pair and not is_co_label
        if is_co_label:
            active_fields = _select_co_fields(active_fields)
        rectification: dict[str, object] | None = None
        if is_config_pair or is_co_label:
            processing_image, rectification = prepare_config_label(image_bytes, image)
            cleaned_image = remove_blue_annotation_pixels(processing_image)
        else:
            cleaned_image = prepare_for_ocr(image, quality, OCR_PREPROCESSING)
        logger.info("Image preprocessing completed")
        if is_co_label:
            logger.info("Targeted CO OCR started; engine=%s", self._provider.name)
            field_candidates, engine_version = self._extract_co_fields(cleaned_image, active_fields)
            lines = []
            raw_text = ""
        elif is_config_pair:
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
            if (is_config_pair or is_co_label) and field_candidates
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
        if is_co_label:
            field_candidates = [
                candidate.model_copy(update={
                    "value": None,
                    "box": None,
                    "status": "NOT_FOUND",
                    "reason": (
                        f"{candidate.reason} The detected value is below the configured "
                        "field confidence threshold."
                    ),
                })
                if candidate.status == "DETECTED"
                and candidate.confidence < thresholds.get(candidate.field_name, confidence_threshold)
                else candidate
                for candidate in field_candidates
            ]
        requires_review = (
            any(
                candidate.status != "DETECTED"
                or candidate.confidence < thresholds.get(candidate.field_name, confidence_threshold)
                for candidate in field_candidates
            )
            or quality.blur_score < MIN_BLUR_SCORE
            or quality.brightness_mean < 25
            or quality.contrast_stddev < 12
            if is_config_pair or is_co_label
            else (
                not readable_lines
                or quality.blur_score < MIN_BLUR_SCORE
                or quality.brightness_mean < 25
                or quality.contrast_stddev < 12
            )
        )
        logger.info("Configured field extraction started; field_count=%d", len(active_fields))
        if not is_config_pair and not is_co_label:
            field_candidates = match_configured_fields(active_fields, readable_lines)
        candidates_by_name = {candidate.field_name: candidate for candidate in field_candidates}
        retry_fields = [
            field for field in active_fields
            if not is_config_pair and not is_co_label
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

    def _extract_co_fields(
        self,
        image: np.ndarray,
        fields: list[ConfiguredField],
    ) -> tuple[list[FieldCandidate], str]:
        candidates_by_kind: dict[str, FieldCandidate] = {}
        engine_versions: list[str] = []
        height, width = image.shape[:2]
        model_field = next(field for field in fields if _normalized_field_name(field.field_name) == "modelno")
        model_left = int(width * 0.04)
        model_top = int(height * 0.08)
        model_right = max(model_left + 1, int(width * 0.68))
        model_bottom = max(model_top + 1, int(height * 0.28))
        model_roi = image[model_top:model_bottom, model_left:model_right]
        model, _, _, model_version = _extract_model_number(
            model_field,
            model_roi,
            self._provider,
            model_left,
            model_top,
        )
        candidates_by_kind["modelno"] = model.model_copy(update={"debug": None})
        engine_versions.append(model_version)

        qr_field = next(field for field in fields if _normalized_field_name(field.field_name) == "qrcode")
        qr_candidate = _extract_co_qr(qr_field, image)
        candidates_by_kind["qrcode"] = qr_candidate

        for field in fields:
            kind = _normalized_field_name(field.field_name)
            if kind in {"serialnumber", "partnumber"}:
                candidate, versions = _extract_co_labeled_value(
                    field,
                    image,
                    self._provider,
                    kind,
                    qr_candidate.value if qr_candidate.status == "DETECTED" else None,
                )
                if kind == "serialnumber":
                    candidate = _cross_check_serial_with_qr(
                        candidate,
                        qr_candidate.value if qr_candidate.status == "DETECTED" else None,
                    )
                candidates_by_kind[kind] = candidate
                engine_versions.extend(versions)

        return (
            [candidates_by_kind[_normalized_field_name(field.field_name)] for field in fields],
            ", ".join(dict.fromkeys(version for version in engine_versions if version)),
        )

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


def _is_co_field_set(fields: list[ConfiguredField]) -> bool:
    names = {_normalized_field_name(field.field_name) for field in fields}
    return {"serialnumber", "partnumber", "modelno", "qrcode"}.issubset(names)


def _select_co_fields(fields: list[ConfiguredField]) -> list[ConfiguredField]:
    required = ("serialnumber", "partnumber", "modelno", "qrcode")
    by_name: dict[str, ConfiguredField] = {}
    for field in fields:
        kind = _normalized_field_name(field.field_name)
        if kind in required:
            if kind in by_name:
                raise ValueError(f"CO field_config contains duplicate {field.field_name} fields.")
            by_name[kind] = field
    if any(name not in by_name for name in required):
        raise ValueError("CO field_config must contain one each of SERIAL_NUMBER, PART_NUMBER, MODEL_NO, and QR_CODE.")
    return [by_name[name] for name in required]


def _extract_co_labeled_value(
    field: ConfiguredField,
    image: np.ndarray,
    provider: OCRProvider,
    kind: str,
    qr_payload: str | None = None,
) -> tuple[FieldCandidate, list[str]]:
    height, width = image.shape[:2]
    top_ratio, bottom_ratio = (0.20, 0.36) if kind == "serialnumber" else (0.29, 0.39)
    top_limit = int(height * top_ratio)
    bottom_limit = int(height * bottom_ratio)
    band_height = max(72, int(height * (0.12 if kind == "serialnumber" else 0.055)))
    step = max(16, int(height * (0.025 if kind == "serialnumber" else 0.0125)))
    left = int(width * 0.05)
    right = max(left + 1, int(width * 0.62))
    label = (
        re.compile(
            r"(?<![A-Z0-9])S\s*/\s*N(?![A-Z0-9])[\s|_:=\-\[\](){}]{0,12}(?P<value>[A-Z0-9]{8,20})(?![A-Z0-9])",
            re.IGNORECASE,
        )
        if kind == "serialnumber"
        else re.compile(
            r"(?<![A-Z0-9])P\s*/\s*N(?![A-Z0-9])\s*[:=]?\s*(?P<value>[A-Z0-9#)\]]{7,18})",
            re.IGNORECASE,
        )
    )
    observed: dict[str, list[tuple[str, float, list[list[float]]]]] = {}
    versions: list[str] = []
    part_number = _part_number_from_qr_payload(qr_payload) if kind == "partnumber" else None
    qr_serial_number = _serial_number_from_qr_payload(qr_payload) if kind == "serialnumber" else None
    for top in range(top_limit, max(top_limit + 1, bottom_limit - band_height + 1), step):
        bottom = min(bottom_limit, top + band_height)
        region = image[top:bottom, left:right]
        gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY) if region.ndim == 3 else region.copy()
        enlarged = cv2.resize(gray, None, fx=3, fy=3, interpolation=cv2.INTER_CUBIC)
        enhanced = cv2.createCLAHE(clipLimit=1.5, tileGridSize=(8, 8)).apply(enlarged)
        variants = (enlarged, enhanced, cv2.threshold(
            enhanced, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU
        )[1])
        for processed in variants:
            result = provider.extract(processed)
            if result.version:
                versions.append(result.version)
            for line in result.lines:
                match = label.search(line.text)
                if match is None:
                    continue
                value = re.sub(r"\s+", "", match.group("value")).strip()
                if kind == "serialnumber":
                    if not re.fullmatch(r"[A-Z0-9]{8,20}", value, re.IGNORECASE):
                        continue
                else:
                    if not re.fullmatch(r"[A-Z0-9]{5,12}#[A-Z0-9]{2,6}", value, re.IGNORECASE):
                        if not part_number or not _matches_qr_confirmed_part_number(value, part_number):
                            continue
                    if part_number:
                        if not _matches_qr_confirmed_part_number(value, part_number):
                            continue
                        value = part_number
                    elif not re.fullmatch(r"[A-Z0-9]{5,12}#[A-Z0-9]{2,6}", value, re.IGNORECASE):
                        continue
                confidence = line.confidence
                box = [
                    [float(left), float(top)],
                    [float(right), float(top)],
                    [float(right), float(bottom)],
                    [float(left), float(bottom)],
                ]
                observed.setdefault(value.casefold(), []).append((value, confidence, box))

    field_label = "S/N" if kind == "serialnumber" else "P/N"
    if qr_serial_number:
        qr_confirmed = [
            detection
            for value, detections in observed.items()
            if _matches_qr_confirmed_serial_number(value, qr_serial_number)
            for detection in detections
        ]
        if qr_confirmed:
            _, confidence, box = max(qr_confirmed, key=lambda detection: detection[1])
            return FieldCandidate(
                field_name=field.field_name,
                value=qr_serial_number,
                confidence=confidence,
                box=box,
                status="DETECTED" if confidence > 0 else "NOT_FOUND",
                reason="The printed S/N was read and cross-checked against the serial number embedded in the decoded QR payload.",
            ), versions

    ranked = sorted(observed.values(), key=lambda detections: max(item[1] for item in detections), reverse=True)
    if ranked and (
        len(ranked) == 1
        or max(item[1] for item in ranked[0]) >= 0.55
        and max(item[1] for item in ranked[0]) - max(item[1] for item in ranked[1]) >= 0.12
    ):
        detections = ranked[0]
        value, _, box = max(detections, key=lambda detection: detection[1])
        confidence = max(item[1] for item in detections)
        return FieldCandidate(
            field_name=field.field_name,
            value=value,
            confidence=confidence,
            box=box,
            status="DETECTED" if confidence > 0 else "NOT_FOUND",
            reason=(
                f"The value beside the {field_label} label was cross-checked against the decoded QR payload."
                if kind == "partnumber" and part_number
                else f"A field-valid value was read beside the {field_label} label in its dedicated region."
            ),
        ), versions
    if len(ranked) > 1:
        return FieldCandidate(
            field_name=field.field_name,
            confidence=0,
            status="AMBIGUOUS",
            reason=f"Multiple distinct field-valid values were read beside the {field_label} label.",
        ), versions
    return FieldCandidate(
        field_name=field.field_name,
        confidence=0,
        status="NOT_FOUND",
        reason=f"No field-valid value was read beside the {field_label} label in its dedicated region.",
    ), versions


def _extract_co_qr(field: ConfiguredField, image: np.ndarray) -> FieldCandidate:
    detector = cv2.QRCodeDetector()
    decoded_values: list[tuple[str, list[list[float]] | None]] = []
    height, width = image.shape[:2]
    left = int(width * 0.42)
    top = 0
    right = width
    bottom = int(height * 0.30)
    region = image[top:bottom, left:right]
    for rotation in range(4):
        rotated = np.rot90(region, rotation).copy() if rotation else region
        decoded, points, _ = detector.detectAndDecode(rotated)
        if decoded:
            box = None
            if points is not None:
                coordinates = points.reshape(-1, 2)
                if rotation == 0:
                    box = [[float(x + left), float(y + top)] for x, y in coordinates]
            decoded_values.append((decoded, box))
        multiple, values, boxes, _ = detector.detectAndDecodeMulti(rotated)
        if multiple:
            for value, points in zip(values, boxes, strict=True):
                if value:
                    box = None
                    if rotation == 0:
                        box = [[float(point[0] + left), float(point[1] + top)] for point in points]
                    decoded_values.append((value, box))
        if decoded_values:
            break

    if not decoded_values:
        decoded_values.extend(_decode_qr_with_zxing(region, left, top))

    values = {value for value, _ in decoded_values}
    if len(values) == 1:
        value = next(iter(values))
        _, box = next(item for item in decoded_values if item[0] == value)
        return FieldCandidate(
            field_name=field.field_name,
            value=value,
            confidence=1,
            box=box,
            status="DETECTED",
            reason="The QR payload was decoded by the QR-code decoder.",
        )
    if len(values) > 1:
        return FieldCandidate(
            field_name=field.field_name,
            confidence=0,
            status="AMBIGUOUS",
            reason="Multiple distinct QR payloads were decoded.",
        )
    return FieldCandidate(
        field_name=field.field_name,
        confidence=0,
        status="NOT_FOUND",
        reason="No QR payload could be decoded.",
    )


def _part_number_from_qr_payload(payload: str | None) -> str | None:
    if not payload:
        return None
    match = re.search(r"(?:[?&])240=([^&#\s]+)", payload, re.IGNORECASE)
    if match is None:
        return None
    part_number = unquote(match.group(1)).upper()
    return part_number if re.fullmatch(r"[A-Z0-9]{5,12}#[A-Z0-9]{2,6}", part_number) else None


def _serial_number_from_qr_payload(payload: str | None) -> str | None:
    if not payload:
        return None
    match = re.search(r"(?:^|/)21/([A-Z0-9]{8,20})(?=[?&#/]|$)", payload, re.IGNORECASE)
    if match is None:
        return None
    return match.group(1).upper()


def _matches_qr_confirmed_serial_number(candidate: str, serial_number: str) -> bool:
    if len(candidate) != len(serial_number):
        return False
    confusions = {
        frozenset(("S", "5")),
        frozenset(("O", "0")),
        frozenset(("I", "1")),
        frozenset(("B", "8")),
        frozenset(("Z", "2")),
        frozenset(("G", "6")),
        frozenset(("J", "3")),
    }
    differences = [
        (observed, expected)
        for observed, expected in zip(candidate.upper(), serial_number.upper(), strict=True)
        if observed != expected
    ]
    return len(differences) <= 1 and all(
        frozenset((observed, expected)) in confusions
        for observed, expected in differences
    )


def _decode_qr_with_zxing(
    region: np.ndarray,
    x_offset: int,
    y_offset: int,
) -> list[tuple[str, list[list[float]] | None]]:
    import zxingcpp

    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY) if region.ndim == 3 else region
    enhanced = cv2.createCLAHE(clipLimit=1.5, tileGridSize=(8, 8)).apply(gray)
    variants = (
        gray,
        enhanced,
        cv2.threshold(enhanced, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)[1],
    )
    decoded: list[tuple[str, list[list[float]] | None]] = []
    seen: set[str] = set()
    for variant in variants:
        for scale in (1, 2):
            candidate = (
                cv2.resize(
                    variant,
                    None,
                    fx=scale,
                    fy=scale,
                    interpolation=cv2.INTER_CUBIC,
                )
                if scale > 1
                else variant
            )
            for result in zxingcpp.read_barcodes(candidate):
                if getattr(result.format, "name", "") != "QRCode" or not result.text or result.text in seen:
                    continue
                seen.add(result.text)
                position = getattr(result, "position", None)
                points = [
                    getattr(position, name, None)
                    for name in ("top_left", "top_right", "bottom_right", "bottom_left")
                ] if position is not None else []
                box = (
                    [
                        [float(point.x) / scale + x_offset, float(point.y) / scale + y_offset]
                        for point in points
                    ]
                    if all(point is not None for point in points)
                    else None
                )
                decoded.append((str(result.text), box))
        if decoded:
            break
    return decoded


def _cross_check_serial_with_qr(
    candidate: FieldCandidate,
    qr_payload: str | None,
) -> FieldCandidate:
    qr_serial_number = _serial_number_from_qr_payload(qr_payload)
    if candidate.status != "DETECTED" or not candidate.value:
        return candidate
    if not qr_serial_number:
        return candidate.model_copy(update={
            "value": None,
            "confidence": 0,
            "status": "AMBIGUOUS",
            "reason": "The printed S/N could not be cross-checked because no serial number was available in the decoded QR payload.",
        })
    if candidate.value.upper() == qr_serial_number:
        return candidate.model_copy(update={
            "reason": "The printed S/N matched the serial number embedded in the decoded QR payload.",
        })
    return candidate.model_copy(update={
        "value": None,
        "confidence": 0,
        "status": "AMBIGUOUS",
        "reason": "The printed S/N does not match the serial number embedded in the decoded QR payload.",
    })


def _matches_qr_confirmed_part_number(candidate: str, part_number: str) -> bool:
    if len(candidate) != len(part_number):
        return False
    confusions = {
        frozenset(("S", "5")),
        frozenset(("O", "0")),
        frozenset(("I", "1")),
        frozenset(("B", "8")),
        frozenset(("H", "#")),
        frozenset((")", "J")),
        frozenset(("]", "J")),
    }
    differences = [
        (observed, expected)
        for observed, expected in zip(candidate.upper(), part_number, strict=True)
        if observed != expected
    ]
    return len(differences) <= 3 and all(
        frozenset((observed, expected)) in confusions
        for observed, expected in differences
    )


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
            normalized_lines = [
                line.text.translate({
                    ord("—"): "-",
                    ord("–"): "-",
                    ord("−"): "-",
                    ord("‑"): "-",
                    ord("‐"): "-",
                }).replace("\ufffd", "")
                for line in result.lines
            ]
            for line_index, line in enumerate(result.lines):
                text = normalized_lines[line_index]
                if not re.search(r"\b\d{2}-[A-Z]{2}\d{4,}[A-Z0-9]{0,8}\b", text, re.IGNORECASE):
                    continue
                if re.search(r"\bHP\s+Laptop\b", text, re.IGNORECASE):
                    combined_text = text
                else:
                    prefix = next(
                        (
                            normalized_lines[index]
                            for index in range(line_index - 1, -1, -1)
                            if re.search(r"\bHP\s+Laptop\b", normalized_lines[index], re.IGNORECASE)
                        ),
                        "",
                    )
                    combined_text = f"{prefix} {text}" if prefix else text
                laptop_match = re.search(r"\bHP\s+Laptop\b", combined_text, re.IGNORECASE)
                model_match = re.search(
                    r"\b\d{2}-[A-Z]{2}\d{4,}[A-Z0-9]{0,8}\b",
                    combined_text,
                    re.IGNORECASE,
                )
                if not laptop_match or not model_match:
                    continue
                value = f"{laptop_match.group(0)} {model_match.group(0)}"
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
