import logging
import os
from io import BytesIO
from typing import Any

import cv2
import numpy as np
from PIL import Image, ImageOps

logger = logging.getLogger(__name__)


def prepare_config_label(
    image_bytes: bytes,
    decoded_image: np.ndarray,
) -> tuple[np.ndarray, dict[str, Any]]:
    image, orientation_applied = _apply_exif_orientation(image_bytes, decoded_image)
    original_height, original_width = image.shape[:2]
    points = _find_label_boundary(image)

    if points is not None:
        rectified = _perspective_transform(image, points)
        source = "detected_boundary"
        corners = [[round(float(x), 2), round(float(y), 2)] for x, y in points]
    else:
        rectified, crop = _fallback_crop(image)
        source = "normalized_fallback"
        corners = None
        logger.info("CONFIG label boundary was not detected; using normalized fallback ROI.")

    rotated = rectified.shape[1] > rectified.shape[0]
    if rotated:
        rectified = cv2.rotate(rectified, cv2.ROTATE_90_CLOCKWISE)

    return rectified, {
        "source": source,
        "corners": corners,
        "exifOrientationApplied": orientation_applied,
        "rotatedToPortrait": rotated,
        "inputWidth": original_width,
        "inputHeight": original_height,
        "rectifiedWidth": int(rectified.shape[1]),
        "rectifiedHeight": int(rectified.shape[0]),
        "fallbackCrop": crop if points is None else None,
    }


def _apply_exif_orientation(
    image_bytes: bytes,
    decoded_image: np.ndarray,
) -> tuple[np.ndarray, bool]:
    try:
        with Image.open(BytesIO(image_bytes)) as source:
            orientation = source.getexif().get(274)
            oriented = ImageOps.exif_transpose(source).convert("RGB")
            return (
                cv2.cvtColor(np.asarray(oriented), cv2.COLOR_RGB2BGR),
                orientation not in (None, 1),
            )
    except (OSError, ValueError):
        logger.warning("Could not read image EXIF orientation; using decoded pixel orientation.")
        return decoded_image.copy(), False


def _find_label_boundary(image: np.ndarray) -> np.ndarray | None:
    height, width = image.shape[:2]
    scale = min(1.0, 1600 / max(height, width))
    working = cv2.resize(
        image,
        None,
        fx=scale,
        fy=scale,
        interpolation=cv2.INTER_AREA,
    ) if scale < 1 else image
    gray = cv2.cvtColor(working, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    median = float(np.median(blurred))
    edges = cv2.Canny(
        blurred,
        max(10, int(0.66 * median)),
        min(255, int(1.33 * median)),
    )
    close_size = max(3, int(min(working.shape[:2]) * 0.012) | 1)
    closed = cv2.morphologyEx(
        edges,
        cv2.MORPH_CLOSE,
        cv2.getStructuringElement(cv2.MORPH_RECT, (close_size, close_size)),
    )
    contours, _ = cv2.findContours(closed, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)

    candidates: list[tuple[float, np.ndarray]] = []
    image_area = float(working.shape[0] * working.shape[1])
    for contour in contours:
        area = cv2.contourArea(contour)
        if area < image_area * 0.08 or area > image_area * 0.98:
            continue
        perimeter = cv2.arcLength(contour, True)
        polygon = cv2.approxPolyDP(contour, 0.02 * perimeter, True)
        if len(polygon) != 4 or not cv2.isContourConvex(polygon):
            continue
        ordered = _order_corners(polygon.reshape(4, 2).astype(np.float32))
        top_width = np.linalg.norm(ordered[1] - ordered[0])
        bottom_width = np.linalg.norm(ordered[2] - ordered[3])
        left_height = np.linalg.norm(ordered[3] - ordered[0])
        right_height = np.linalg.norm(ordered[2] - ordered[1])
        if min(top_width, bottom_width, left_height, right_height) < min(working.shape[:2]) * 0.08:
            continue
        ratio = max(top_width, bottom_width) / max(left_height, right_height)
        if not 0.18 <= ratio <= 3.5:
            continue
        candidates.append((area, ordered))

    if not candidates:
        return None

    _, points = max(candidates, key=lambda candidate: candidate[0])
    if scale < 1:
        points /= scale
    points[:, 0] = np.clip(points[:, 0], 0, width - 1)
    points[:, 1] = np.clip(points[:, 1], 0, height - 1)
    return points


def _order_corners(points: np.ndarray) -> np.ndarray:
    ordered = np.zeros((4, 2), dtype=np.float32)
    sums = points.sum(axis=1)
    differences = np.diff(points, axis=1).reshape(-1)
    ordered[0] = points[np.argmin(sums)]
    ordered[2] = points[np.argmax(sums)]
    ordered[1] = points[np.argmin(differences)]
    ordered[3] = points[np.argmax(differences)]
    return ordered


def _perspective_transform(image: np.ndarray, points: np.ndarray) -> np.ndarray:
    top_left, top_right, bottom_right, bottom_left = points
    width = max(
        np.linalg.norm(bottom_right - bottom_left),
        np.linalg.norm(top_right - top_left),
    )
    height = max(
        np.linalg.norm(top_right - bottom_right),
        np.linalg.norm(top_left - bottom_left),
    )
    output_width = max(2, round(float(width)))
    output_height = max(2, round(float(height)))
    target = np.array(
        [
            [0, 0],
            [output_width - 1, 0],
            [output_width - 1, output_height - 1],
            [0, output_height - 1],
        ],
        dtype=np.float32,
    )
    transform = cv2.getPerspectiveTransform(points.astype(np.float32), target)
    return cv2.warpPerspective(
        image,
        transform,
        (output_width, output_height),
        flags=cv2.INTER_CUBIC,
        borderMode=cv2.BORDER_REPLICATE,
    )


def _fallback_crop(image: np.ndarray) -> tuple[np.ndarray, dict[str, float]]:
    configured = os.getenv("CONFIG_FALLBACK_ROI", "0,0,1,1")
    try:
        values = [float(value.strip()) for value in configured.split(",")]
    except ValueError as error:
        raise ValueError("CONFIG_FALLBACK_ROI must be normalized x,y,width,height values.") from error
    if (
        len(values) != 4
        or any(value < 0 or value > 1 for value in values)
        or values[2] <= 0
        or values[3] <= 0
        or values[0] + values[2] > 1
        or values[1] + values[3] > 1
    ):
        raise ValueError(
            "CONFIG_FALLBACK_ROI must be a normalized x,y,width,height rectangle inside the image."
        )
    height, width = image.shape[:2]
    x1 = round(values[0] * width)
    y1 = round(values[1] * height)
    x2 = min(width, max(x1 + 1, round((values[0] + values[2]) * width)))
    y2 = min(height, max(y1 + 1, round((values[1] + values[3]) * height)))
    return image[y1:y2, x1:x2].copy(), {
        "x": values[0],
        "y": values[1],
        "width": values[2],
        "height": values[3],
    }
