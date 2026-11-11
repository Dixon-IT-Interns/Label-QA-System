import cv2
import numpy as np

from app.schemas.ocr import ImageQuality


def prepare_for_ocr(
    image: np.ndarray,
    quality: ImageQuality,
    method: str = "auto",
) -> np.ndarray:
    image = remove_blue_annotation_pixels(image)
    if method == "original":
        return image.copy()

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    if method == "grayscale":
        return gray
    if method == "clahe" or (
        method == "auto" and (quality.contrast_stddev < 20 or quality.brightness_mean < 75)
    ):
        enhanced = cv2.createCLAHE(clipLimit=1.5, tileGridSize=(8, 8)).apply(gray)
        return enhanced if method == "clahe" else cv2.cvtColor(enhanced, cv2.COLOR_GRAY2BGR)
    if method == "otsu":
        blurred = cv2.GaussianBlur(gray, (3, 3), 0)
        return cv2.threshold(blurred, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)[1]
    if method == "adaptive":
        return cv2.adaptiveThreshold(
            cv2.GaussianBlur(gray, (3, 3), 0),
            255,
            cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
            cv2.THRESH_BINARY,
            31,
            11,
        )
    if method == "auto":
        return image.copy()
    raise ValueError(f"Unsupported OCR preprocessing method: {method}")


def has_blue_annotation_like_marks(image: np.ndarray) -> bool:
    blue_pixels = _blue_annotation_mask(image)
    return cv2.countNonZero(blue_pixels) >= max(20, image.shape[0] * image.shape[1] // 10000)


def remove_blue_annotation_pixels(image: np.ndarray) -> np.ndarray:
    cleaned = image.copy()
    mask = _blue_annotation_mask(image)
    if cv2.countNonZero(mask) >= max(20, image.shape[0] * image.shape[1] // 10000):
        cleaned[mask > 0] = 255
    return cleaned


def _blue_annotation_mask(image: np.ndarray) -> np.ndarray:
    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    return cv2.inRange(
        hsv,
        np.array([90, 50, 45], dtype=np.uint8),
        np.array([140, 255, 255], dtype=np.uint8),
    )
