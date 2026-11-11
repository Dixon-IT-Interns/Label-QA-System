import importlib.metadata
import json
import logging
import threading
from typing import Any

import numpy as np

from app.config import OCR_CPU_THREADS
from app.ocr.provider import OCRProvider, OCRResult, OCRUnavailableError
from app.schemas.ocr import OCRLine

logger = logging.getLogger(__name__)


class PaddleOCRProvider(OCRProvider):
    def __init__(self) -> None:
        self._engine: Any | None = None
        self._lock = threading.Lock()
        self._inference_lock = threading.Lock()

    @property
    def name(self) -> str:
        return "PaddleOCR"

    def extract(self, image: np.ndarray) -> OCRResult:
        engine = self._get_engine()
        with self._inference_lock:
            predictions = engine.predict(image)
        lines: list[OCRLine] = []

        for prediction in predictions or []:
            data = self._prediction_data(prediction)
            texts = data.get("rec_texts")
            scores = data.get("rec_scores")
            boxes = data.get("rec_polys")
            if texts is None:
                texts = []
            if scores is None:
                scores = []
            if boxes is None:
                boxes = data.get("dt_polys")
            if boxes is None:
                boxes = []

            for index, text in enumerate(texts):
                if not isinstance(text, str) or not text.strip():
                    continue
                score = float(scores[index]) if index < len(scores) else 0.0
                polygon = boxes[index] if index < len(boxes) else None
                lines.append(
                    OCRLine(
                        text=text.strip(),
                        confidence=max(0.0, min(1.0, score)),
                        box=self._to_box(polygon),
                    )
                )

        return OCRResult(
            version=self._version(),
            raw_text="\n".join(line.text for line in lines),
            lines=lines,
        )

    def _get_engine(self) -> Any:
        if self._engine is not None:
            return self._engine

        with self._lock:
            if self._engine is not None:
                return self._engine

            try:
                from paddleocr import PaddleOCR
            except ImportError as error:
                logger.exception("PaddleOCR dependencies are not installed")
                raise OCRUnavailableError(
                    "PaddleOCR is not installed. Install the OCR service requirements to enable extraction."
                ) from error

            try:
                self._engine = PaddleOCR(
                    lang="en",
                    ocr_version="PP-OCRv6",
                    use_doc_orientation_classify=True,
                    use_doc_unwarping=False,
                    use_textline_orientation=True,
                    device="cpu",
                    enable_mkldnn=False,
                    cpu_threads=OCR_CPU_THREADS,
                )
            except Exception as error:
                logger.exception("PaddleOCR engine initialization failed")
                raise OCRUnavailableError(
                    "PaddleOCR could not initialize. Check model availability and service logs."
                ) from error

        return self._engine

    @staticmethod
    def _prediction_data(prediction: Any) -> dict[str, Any]:
        if isinstance(prediction, dict):
            data = prediction
        else:
            serialized = getattr(prediction, "json", None)
            if callable(serialized):
                serialized = serialized()
            if isinstance(serialized, str):
                serialized = json.loads(serialized)
            data = serialized if isinstance(serialized, dict) else {}
        nested = data.get("res")
        return nested if isinstance(nested, dict) else data

    @staticmethod
    def _to_box(polygon: Any) -> list[list[float]] | None:
        if polygon is None:
            return None
        try:
            return [[float(point[0]), float(point[1])] for point in polygon]
        except (TypeError, ValueError, IndexError):
            return None

    def _version(self) -> str:
        try:
            return importlib.metadata.version("paddleocr")
        except importlib.metadata.PackageNotFoundError:
            return "unknown"
