import os
from pathlib import Path


MAX_UPLOAD_BYTES = int(os.getenv("OCR_MAX_UPLOAD_BYTES", str(20 * 1024 * 1024)))
MIN_IMAGE_WIDTH = int(os.getenv("OCR_MIN_IMAGE_WIDTH", "320"))
MIN_IMAGE_HEIGHT = int(os.getenv("OCR_MIN_IMAGE_HEIGHT", "240"))
MIN_BLUR_SCORE = float(os.getenv("OCR_MIN_BLUR_SCORE", "35"))
MIN_OCR_CONFIDENCE = float(os.getenv("OCR_MIN_CONFIDENCE", "0.85"))
OCR_CPU_THREADS = max(1, int(os.getenv("OCR_CPU_THREADS", "2")))
OCR_ENGINE = os.getenv("OCR_ENGINE", "tesseract").strip().casefold()
OCR_PREPROCESSING = os.getenv("OCR_PREPROCESSING", "auto").strip().casefold()
TESSERACT_CMD = os.getenv("TESSERACT_CMD") or None
if TESSERACT_CMD is None:
    windows_tesseract = Path(r"C:\Program Files\Tesseract-OCR\tesseract.exe")
    if windows_tesseract.is_file():
        TESSERACT_CMD = str(windows_tesseract)
TESSERACT_LANGUAGE = os.getenv("TESSERACT_LANGUAGE", "eng")
TESSERACT_CONFIG = os.getenv("TESSERACT_CONFIG", "--oem 3 --psm 6")
OCR_ARTIFACTS_DIR = Path(
    os.getenv(
        "OCR_ARTIFACTS_DIR",
        str(Path(__file__).resolve().parents[2] / "storage" / "ocr"),
    )
)
if OCR_ENGINE not in {"tesseract", "paddle"}:
    raise ValueError("OCR_ENGINE must be either 'tesseract' or 'paddle'.")
if OCR_PREPROCESSING not in {"auto", "original", "grayscale", "clahe", "otsu", "adaptive"}:
    raise ValueError(
        "OCR_PREPROCESSING must be one of: auto, original, grayscale, clahe, otsu, adaptive."
    )

ALLOWED_IMAGE_TYPES = {
    "image/jpeg": {".jpg", ".jpeg"},
    "image/png": {".png"},
    "image/webp": {".webp"},
}
