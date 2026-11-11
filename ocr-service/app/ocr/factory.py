from app.config import OCR_ENGINE
from app.ocr.provider import OCRProvider
from app.ocr.tesseract_provider import TesseractOCRProvider


def create_ocr_provider() -> OCRProvider:
    if OCR_ENGINE == "tesseract":
        return TesseractOCRProvider()
    if OCR_ENGINE == "paddle":
        from app.ocr.paddle_provider import PaddleOCRProvider

        return PaddleOCRProvider()
    raise ValueError(f"Unsupported OCR engine configured: {OCR_ENGINE}")
