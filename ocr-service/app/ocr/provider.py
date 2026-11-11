from abc import ABC, abstractmethod
from dataclasses import dataclass

import numpy as np

from app.schemas.ocr import OCRLine


class OCRUnavailableError(RuntimeError):
    pass


@dataclass(frozen=True)
class OCRResult:
    version: str
    raw_text: str
    lines: list[OCRLine]


class OCRProvider(ABC):
    @property
    @abstractmethod
    def name(self) -> str:
        raise NotImplementedError

    @abstractmethod
    def extract(self, image: np.ndarray) -> OCRResult:
        raise NotImplementedError

    def extract_with_config(self, image: np.ndarray, config: str) -> OCRResult:
        return self.extract(image)
