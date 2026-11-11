import logging
import threading
from collections import defaultdict
from typing import Any

import numpy as np

from app.config import TESSERACT_CMD, TESSERACT_CONFIG, TESSERACT_LANGUAGE
from app.ocr.provider import OCRProvider, OCRResult, OCRUnavailableError
from app.schemas.ocr import OCRLine

logger = logging.getLogger(__name__)


class TesseractOCRProvider(OCRProvider):
    def __init__(self) -> None:
        self._pytesseract: Any | None = None
        self._lock = threading.Lock()
        self._inference_lock = threading.Lock()

    @property
    def name(self) -> str:
        return "Tesseract"

    def extract(self, image: np.ndarray) -> OCRResult:
        return self.extract_with_config(image, TESSERACT_CONFIG)

    def extract_with_config(self, image: np.ndarray, config: str) -> OCRResult:
        pytesseract = self._get_pytesseract()
        try:
            with self._inference_lock:
                raw_text = pytesseract.image_to_string(
                    image,
                    lang=TESSERACT_LANGUAGE,
                    config=config,
                )
                data = pytesseract.image_to_data(
                    image,
                    lang=TESSERACT_LANGUAGE,
                    config=config,
                    output_type=pytesseract.Output.DICT,
                )
                version = str(pytesseract.get_tesseract_version())
        except pytesseract.pytesseract.TesseractNotFoundError as error:
            logger.exception("Tesseract executable was not found")
            raise OCRUnavailableError(
                "Tesseract OCR is not installed or TESSERACT_CMD points to an invalid executable."
            ) from error
        except Exception as error:
            logger.exception("Tesseract OCR execution failed")
            raise RuntimeError("Tesseract OCR execution failed; see service logs.") from error

        grouped: dict[tuple[int, int, int, int], list[tuple[str, float, int, int, int, int]]] = (
            defaultdict(list)
        )
        for index, text in enumerate(data.get("text", [])):
            text = str(text).strip()
            confidence = self._number(data, "conf", index)
            if not text or confidence < 0:
                continue
            key = tuple(
                int(self._number(data, key, index))
                for key in ("page_num", "block_num", "par_num", "line_num")
            )
            grouped[key].append(
                (
                    text,
                    max(0.0, min(100.0, confidence)) / 100,
                    int(self._number(data, "left", index)),
                    int(self._number(data, "top", index)),
                    int(self._number(data, "width", index)),
                    int(self._number(data, "height", index)),
                )
            )

        lines: list[OCRLine] = []
        for words in grouped.values():
            left = min(word[2] for word in words)
            top = min(word[3] for word in words)
            right = max(word[2] + word[4] for word in words)
            bottom = max(word[3] + word[5] for word in words)
            lines.append(
                OCRLine(
                    text=" ".join(word[0] for word in words),
                    confidence=min(
                        1.0,
                        sum(word[1] for word in words) / len(words),
                    ),
                    box=[
                        [float(left), float(top)],
                        [float(right), float(top)],
                        [float(right), float(bottom)],
                        [float(left), float(bottom)],
                    ],
                )
            )

        return OCRResult(version=version, raw_text=raw_text, lines=lines)

    def _get_pytesseract(self) -> Any:
        if self._pytesseract is not None:
            return self._pytesseract
        with self._lock:
            if self._pytesseract is not None:
                return self._pytesseract
            try:
                import pytesseract
            except ImportError as error:
                logger.exception("pytesseract is not installed")
                raise OCRUnavailableError(
                    "pytesseract is not installed. Install the OCR service requirements."
                ) from error
            if TESSERACT_CMD:
                pytesseract.pytesseract.tesseract_cmd = TESSERACT_CMD
            self._pytesseract = pytesseract
        return self._pytesseract

    @staticmethod
    def _number(data: dict[str, Any], key: str, index: int) -> float:
        values = data.get(key, [])
        if index >= len(values):
            return 0
        try:
            return float(values[index])
        except (TypeError, ValueError):
            return 0
