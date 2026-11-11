import logging
import os
import re
from collections import defaultdict
from dataclasses import dataclass
from typing import Any

import cv2
import numpy as np

from app.config import TESSERACT_CONFIG
from app.ocr.provider import OCRProvider, OCRResult
from app.schemas.ocr import FieldCandidate, OCRLine

logger = logging.getLogger(__name__)

_QUEUE_PATTERN = re.compile(
    os.getenv("CONFIG_QUEUE_PATTERN", r"^[A-Z0-9]+#[A-Z0-9]+$"),
    re.IGNORECASE,
)
_QUEUE_IN_TEXT = re.compile(
    r"(?<![A-Z0-9])([A-Z0-9]{1,32}\s*#\s*[A-Z0-9]{1,24})(?![A-Z0-9])",
    re.IGNORECASE,
)
_QUEUE_FIELD = "QUEUE_NO"


@dataclass(frozen=True)
class _Barcode:
    value: str | None
    format: str
    box: tuple[int, int, int, int] | None


@dataclass(frozen=True)
class _Pass:
    variant: str
    psm: int
    result: OCRResult


def extract_queue_number(
    image: np.ndarray,
    provider: OCRProvider,
    barcode_image: np.ndarray | None = None,
) -> FieldCandidate:
    height, width = image.shape[:2]
    barcodes = _detect_barcodes(barcode_image if barcode_image is not None else image)
    barcode = _select_barcode(barcodes, width, height)
    if barcode is None:
        barcode = _locate_linear_barcode_region(image)
    roi, roi_box, roi_source = _queue_roi(image, barcode)
    variants = _preprocessing_variants(roi)
    passes = _run_ocr_passes(variants, provider)
    candidates = _collect_candidates(passes)
    normalized_barcode = (
        _normalize_candidate(barcode.value)
        if barcode and barcode.value
        else None
    )
    selected, selected_confidence, selected_source = _select_candidate(
        candidates,
        normalized_barcode,
    )
    debug_candidates = [
        {
            "value": value,
            "confidence": round(100 * entry["confidence"]),
            "frequency": len(entry["passes"]),
            "variantFrequency": len({vote["variant"] for vote in entry["passes"]}),
        }
        for value, entry in sorted(
            candidates.items(),
            key=lambda item: (-len(item[1]["passes"]), item[0]),
        )
    ]
    debug: dict[str, Any] = {
        "field": "Queue No.",
        "roiDetected": roi_source != "profile_fallback",
        "roi": {
            "x": roi_box[0],
            "y": roi_box[1],
            "width": roi_box[2],
            "height": roi_box[3],
        },
        "roiSource": roi_source,
        "preprocessingVariants": len(variants),
        "ocrPasses": len(passes),
        "candidateCount": len(candidates),
        "barcode": (
            {
                "format": barcode.format,
                "value": barcode.value,
                "box": list(barcode.box) if barcode.box else None,
            }
            if barcode
            else None
        ),
        "candidates": debug_candidates,
        "selected": selected,
        "selectedValue": selected,
        "selectionSource": selected_source,
        "passResults": [
            {
                "variant": pass_result.variant,
                "psm": pass_result.psm,
                "text": pass_result.result.raw_text.strip(),
                "confidence": round(
                    100 * max(
                        (line.confidence for line in pass_result.result.lines),
                        default=0,
                    )
                ),
            }
            for pass_result in passes
        ],
    }

    unique_lines = _unique_lines(passes)
    if selected is not None:
        return FieldCandidate(
            field_name=_QUEUE_FIELD,
            value=selected,
            confidence=selected_confidence,
            box=_line_box_for_value(unique_lines, selected),
            status="DETECTED",
            reason=(
                "Queue No. was resolved from dedicated ROI OCR candidates"
                + (" and cross-validated against the decoded linear barcode." if selected_source == "barcode_cross_validated" else ".")
            ),
            debug=debug,
        )
    return FieldCandidate(
        field_name=_QUEUE_FIELD,
        confidence=selected_confidence,
        status="AMBIGUOUS" if candidates else "NOT_FOUND",
        reason=(
            "Queue No. OCR candidates could not be resolved with sufficient agreement."
            if candidates
            else "No valid Queue No. candidate was detected in the dedicated below-barcode region."
        ),
        debug=debug,
    )


def _detect_barcodes(image: np.ndarray) -> list[_Barcode]:
    try:
        import zxingcpp
    except ImportError:
        logger.warning("Barcode reader unavailable; Queue No. OCR will use the configured fallback ROI.")
        return []

    barcodes = []
    for result in zxingcpp.read_barcodes(image):
        barcode_format = str(result.format)
        if any(name in barcode_format.casefold() for name in ("qr", "datamatrix", "pdf417", "aztec")):
            continue
        value = _normalize_candidate(str(result.text))
        if not _QUEUE_PATTERN.fullmatch(value):
            continue
        position = getattr(result, "position", None)
        box = None
        if position is not None:
            points = [
                getattr(position, name, None)
                for name in ("top_left", "top_right", "bottom_right", "bottom_left")
            ]
            if all(point is not None for point in points):
                xs = [float(point.x) for point in points]
                ys = [float(point.y) for point in points]
                box = (
                    int(min(xs)),
                    int(min(ys)),
                    int(max(xs) - min(xs)),
                    int(max(ys) - min(ys)),
                )
        barcodes.append(_Barcode(value=value, format=barcode_format, box=box))
    return barcodes


def _select_barcode(barcodes: list[_Barcode], width: int, height: int) -> _Barcode | None:
    located = [
        barcode
        for barcode in barcodes
        if barcode.box is not None
        and barcode.box[0] + barcode.box[2] / 2 >= width * 0.15
        and barcode.box[1] + barcode.box[3] / 2 >= height * 0.55
        and barcode.box[2] >= barcode.box[3] * 1.2
    ]
    return max(
        located,
        key=lambda barcode: barcode.box[1] + barcode.box[3],
        default=None,
    )


def _locate_linear_barcode_region(image: np.ndarray) -> _Barcode | None:
    height, width = image.shape[:2]
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if image.ndim == 3 else image
    search_top = int(height * 0.72)
    search_bottom = int(height * 0.93)
    search = gray[search_top:search_bottom, :]
    gradient = cv2.convertScaleAbs(cv2.Sobel(search, cv2.CV_32F, 1, 0, ksize=3))
    binary = cv2.threshold(gradient, 30, 255, cv2.THRESH_BINARY)[1]
    kernel_width = max(15, int(width * 0.035))
    closed = cv2.morphologyEx(
        binary,
        cv2.MORPH_CLOSE,
        cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_width, 5)),
    )
    closed = cv2.erode(closed, None, iterations=1)
    closed = cv2.dilate(closed, None, iterations=2)
    contours, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    boxes = []
    for contour in contours:
        left, top, box_width, box_height = cv2.boundingRect(contour)
        top += search_top
        if (
            box_width < width * 0.35
            or box_height < max(12, height * 0.02)
            or box_height > height * 0.08
            or box_width / box_height < 3
            or top + box_height > height * 0.9
        ):
            continue
        boxes.append((left, top, box_width, box_height))

    if not boxes:
        return None
    box = max(boxes, key=lambda item: item[2] * item[3])
    return _Barcode(value=None, format="linear_barcode_region", box=box)


def _locate_queue_text_region(
    image: np.ndarray,
    barcode_box: tuple[int, int, int, int] | None,
) -> tuple[int, int, int, int] | None:
    height, width = image.shape[:2]
    if barcode_box:
        barcode_left, barcode_top, barcode_width, barcode_height = barcode_box
        search_top = barcode_top + barcode_height
        search_bottom = min(height, search_top + int(height * 0.08))
    else:
        barcode_left = int(width * 0.1)
        barcode_width = int(width * 0.65)
        search_top = int(height * 0.75)
        search_bottom = int(height * 0.94)

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if image.ndim == 3 else image
    search = gray[search_top:search_bottom, :]
    if search.size == 0:
        return None
    gradient = cv2.convertScaleAbs(cv2.Sobel(search, cv2.CV_32F, 1, 0, ksize=3))
    binary = cv2.threshold(gradient, 30, 255, cv2.THRESH_BINARY)[1]
    kernel_width = max(15, int(width * 0.035))
    closed = cv2.morphologyEx(
        binary,
        cv2.MORPH_CLOSE,
        cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_width, 5)),
    )
    closed = cv2.erode(closed, None, iterations=1)
    closed = cv2.dilate(closed, None, iterations=2)
    contours, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    boxes = []
    for contour in contours:
        left, top, box_width, box_height = cv2.boundingRect(contour)
        top += search_top
        center_x = left + box_width / 2
        if (
            box_height < height * 0.012
            or box_height > height * 0.05
            or box_width / box_height < 3
        ):
            continue
        if barcode_box:
            barcode_center = barcode_left + barcode_width / 2
            if (
                not barcode_width * 0.18 <= box_width <= barcode_width * 0.75
                or abs(center_x - barcode_center) > barcode_width * 0.35
            ):
                continue
        elif (
            not width * 0.15 <= box_width <= width * 0.48
            or not width * 0.28 <= center_x <= width * 0.5
        ):
            continue
        boxes.append((left, top, box_width, box_height))

    if not boxes:
        return None
    return min(boxes, key=lambda item: item[1])


def _queue_roi(
    image: np.ndarray,
    barcode: _Barcode | None,
) -> tuple[np.ndarray, tuple[int, int, int, int], str]:
    height, width = image.shape[:2]
    text_box = _locate_queue_text_region(image, barcode.box if barcode else None)
    if text_box:
        text_left, text_top, text_width, text_height = text_box
        margin = max(3, int(text_height * 0.09))
        x1 = max(0, text_left - margin)
        y1 = max(0, text_top - margin)
        x2 = min(width, text_left + text_width + margin)
        y2 = min(height, text_top + text_height + margin)
        return (
            image[y1:y2, x1:x2].copy(),
            (x1, y1, x2 - x1, y2 - y1),
            "barcode_text_region" if barcode and barcode.box else "profile_text_region",
        )

    if barcode and barcode.box:
        left, top, barcode_width, barcode_height = barcode.box
        margin_x = int(barcode_width * 0.12)
        x1 = max(0, left + margin_x)
        x2 = min(width, left + barcode_width - margin_x)
        y1 = min(height, top + barcode_height + max(3, int(height * 0.003)))
        text_height = max(int(height * 0.045), int(barcode_height * 0.5))
        y2 = min(height, y1 + text_height)
        if y2 - y1 >= 8 and x2 - x1 >= 16:
            return (
                image[y1:y2, x1:x2].copy(),
                (x1, y1, x2 - x1, y2 - y1),
                "barcode_below_region",
            )

    x1, x2 = int(width * 0.18), int(width * 0.6)
    y1, y2 = int(height * 0.84), int(height * 0.91)
    return (
        image[y1:y2, x1:x2].copy(),
        (x1, y1, x2 - x1, y2 - y1),
        "profile_fallback",
    )


def _preprocessing_variants(roi: np.ndarray) -> dict[str, np.ndarray]:
    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY) if roi.ndim == 3 else roi.copy()
    upscale = min(4.0, max(1.0, 1200 / max(gray.shape[:2])))
    upscaled = cv2.resize(gray, None, fx=upscale, fy=upscale, interpolation=cv2.INTER_CUBIC)
    mild_contrast = cv2.convertScaleAbs(upscaled, alpha=1.12, beta=4)
    clahe = cv2.createCLAHE(clipLimit=1.8, tileGridSize=(8, 8)).apply(upscaled)
    denoised = cv2.fastNlMeansDenoising(clahe, None, h=5, templateWindowSize=7, searchWindowSize=21)
    otsu = cv2.threshold(upscaled, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)[1]
    block_size = min(31, min(upscaled.shape[:2]) if min(upscaled.shape[:2]) % 2 else min(upscaled.shape[:2]) - 1)
    block_size = max(3, block_size)
    adaptive = cv2.adaptiveThreshold(
        upscaled,
        255,
        cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        cv2.THRESH_BINARY,
        block_size,
        5,
    )
    adaptive_mean = cv2.adaptiveThreshold(
        upscaled,
        255,
        cv2.ADAPTIVE_THRESH_MEAN_C,
        cv2.THRESH_BINARY,
        block_size,
        5,
    )
    blurred = cv2.GaussianBlur(upscaled, (0, 0), 0.8)
    sharpened = cv2.addWeighted(upscaled, 1.2, blurred, -0.2, 0)
    sharpened_otsu = cv2.threshold(
        sharpened,
        0,
        255,
        cv2.THRESH_BINARY + cv2.THRESH_OTSU,
    )[1]
    return {
        "contrast": mild_contrast,
        "clahe_denoise": denoised,
        "otsu": otsu,
        "adaptive": adaptive,
        "adaptive_mean": adaptive_mean,
        "sharpened_otsu": sharpened_otsu,
    }


def _run_ocr_passes(
    variants: dict[str, np.ndarray],
    provider: OCRProvider,
) -> list[_Pass]:
    passes = []
    for variant_name, image in variants.items():
        for psm in (7, 6, 13):
            config = re.sub(r"--psm\s+\d+", f"--psm {psm}", TESSERACT_CONFIG)
            if config == TESSERACT_CONFIG and not re.search(r"--psm\s+\d+", config):
                config = f"{config} --psm {psm}".strip()
            result = provider.extract_with_config(image, config)
            passes.append(_Pass(variant_name, psm, result))
    return passes


def _collect_candidates(passes: list[_Pass]) -> dict[str, dict[str, Any]]:
    candidates: dict[str, dict[str, Any]] = defaultdict(
        lambda: {"confidences": [], "passes": [], "boxes": []}
    )
    for pass_result in passes:
        for line in pass_result.result.lines:
            for match in _QUEUE_IN_TEXT.finditer(line.text.upper()):
                value = _normalize_candidate(match.group(1))
                if not _QUEUE_PATTERN.fullmatch(value):
                    continue
                entry = candidates[value]
                entry["confidences"].append(line.confidence)
                entry["passes"].append(
                    {"variant": pass_result.variant, "psm": pass_result.psm}
                )
                if line.box:
                    entry["boxes"].append(line.box)
    for entry in candidates.values():
        entry["confidence"] = sum(entry["confidences"]) / len(entry["confidences"])
    return dict(candidates)


def _select_candidate(
    candidates: dict[str, dict[str, Any]],
    barcode_value: str | None,
) -> tuple[str | None, float, str | None]:
    if not candidates:
        return None, 0, None

    if barcode_value and barcode_value in candidates:
        entry = candidates[barcode_value]
        confidence = min(1.0, 0.65 * entry["confidence"] + 0.35)
        return barcode_value, confidence, "barcode_cross_validated"

    if barcode_value:
        near_matches = [
            (value, entry)
            for value, entry in candidates.items()
            if _edit_distance(value, barcode_value) == 1
        ]
        if len(near_matches) == 1:
            _, entry = near_matches[0]
            confidence = min(1.0, 0.65 * entry["confidence"] + 0.35)
            return barcode_value, confidence, "barcode_cross_validated"

    def candidate_score(entry: dict[str, Any]) -> float:
        variant_count = len({vote["variant"] for vote in entry["passes"]})
        return 0.65 * entry["confidence"] + 0.35 * min(1.0, variant_count / 3)

    ranked = sorted(
        candidates.items(),
        key=lambda item: (
            candidate_score(item[1]),
            len({entry["variant"] for entry in item[1]["passes"]}),
            len(item[1]["passes"]),
        ),
        reverse=True,
    )
    value, entry = ranked[0]
    variant_frequency = len({vote["variant"] for vote in entry["passes"]})
    runner_up_score = candidate_score(ranked[1][1]) if len(ranked) > 1 else 0
    score = candidate_score(entry)
    consensus = variant_frequency >= 2 and score > runner_up_score
    confidence = min(1.0, 0.65 * entry["confidence"] + 0.35 * min(1, variant_frequency / 3))
    if consensus and confidence >= 0.55:
        return value, confidence, "ocr_consensus"
    return None, confidence, None


def _normalize_candidate(value: str) -> str:
    return re.sub(r"\s+", "", value).upper()


def _edit_distance(left: str, right: str) -> int:
    previous = list(range(len(right) + 1))
    for row, left_character in enumerate(left, start=1):
        current = [row]
        for column, right_character in enumerate(right, start=1):
            current.append(min(
                current[-1] + 1,
                previous[column] + 1,
                previous[column - 1] + (left_character != right_character),
            ))
        previous = current
    return previous[-1]


def _unique_lines(passes: list[_Pass]) -> list[OCRLine]:
    unique: dict[tuple[str, tuple[tuple[float, float], ...] | None], OCRLine] = {}
    for pass_result in passes:
        for line in pass_result.result.lines:
            box = tuple((point[0], point[1]) for point in line.box) if line.box else None
            key = (line.text, box)
            if key not in unique or line.confidence > unique[key].confidence:
                unique[key] = line
    return list(unique.values())


def _line_box_for_value(
    lines: list[OCRLine],
    selected: str,
) -> list[list[float]] | None:
    matching = [
        line for line in lines
        if any(
            _normalize_candidate(match.group(1)) == selected
            for match in _QUEUE_IN_TEXT.finditer(line.text.upper())
        )
    ]
    best = max(matching, key=lambda line: line.confidence, default=None)
    return best.box if best else None
