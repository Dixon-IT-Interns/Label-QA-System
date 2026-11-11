import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import cv2
import numpy as np
from fastapi import FastAPI
from fastapi.testclient import TestClient

from app.api.routes import router
from app.config import TESSERACT_CMD
from app.ocr.provider import OCRProvider, OCRResult
from app.ocr.tesseract_provider import TesseractOCRProvider
from app.schemas.ocr import OCRLine
from app.services.artifact_storage import OCRArtifactStore
from app.services.ocr_service import OCRService


class _ConfigUploadProvider(OCRProvider):
    def __init__(self) -> None:
        self.calls = 0

    @property
    def name(self) -> str:
        return "config-upload-test-provider"

    def extract(self, image: np.ndarray) -> OCRResult:
        self.calls += 1
        text = "HP Laptop 15-fd1654TU" if self.calls <= 18 else "E52PWPA#ACJ"
        line = OCRLine(text=text, confidence=0.99)
        return OCRResult(version="test", raw_text=text, lines=[line])


def _tesseract_available() -> bool:
    return bool(
        TESSERACT_CMD and Path(TESSERACT_CMD).is_file()
        or shutil.which("tesseract")
    )


def _make_label(model: str) -> bytes:
    image = np.full((900, 1200, 3), 255, dtype=np.uint8)
    lines = [
        ("HP", 70, 95, 1.4),
        ("MODEL", 70, 240, 1.5),
        (model, 70, 315, 1.25),
        ("PROCESSOR", 70, 440, 1.25),
        ("Intel Core 5 120U", 70, 515, 1.0),
        ("Made in India  Warning", 70, 760, 0.8),
    ]
    for text, x, y, scale in lines:
        cv2.putText(
            image,
            text,
            (x, y),
            cv2.FONT_HERSHEY_SIMPLEX,
            scale,
            (0, 0, 0),
            3 if scale >= 1 else 2,
            cv2.LINE_AA,
        )
    encoded, buffer = cv2.imencode(".png", image)
    if not encoded:
        raise RuntimeError("Could not encode the generated OCR label.")
    return buffer.tobytes()


def _make_config_label() -> bytes:
    image = np.full((1600, 1200, 3), 255, dtype=np.uint8)
    lines = [
        ("HP Laptop 15-fd1654TU", (120, 185), 1.4),
        ("Intel Core 5 120U Processor", (120, 500), 1.1),
        ("512GB Solid State Drive", (120, 620), 1.1),
        ("8GB SODIMM DDR4 SDRAM", (120, 740), 1.1),
        ("Windows 11", (120, 860), 1.1),
        ("15.6 FHD LED Display 250 nits", (120, 980), 1.1),
        ("3-cell 41 Wh", (120, 1100), 1.1),
        ("E52PWPA#ACJ", (500, 1490), 0.8),
    ]
    for text, origin, scale in lines:
        cv2.putText(
            image,
            text,
            origin,
            cv2.FONT_HERSHEY_SIMPLEX,
            scale,
            (0, 0, 0),
            2 if text == "E52PWPA#ACJ" else 3,
            cv2.LINE_AA,
        )
    for x in range(160, 1040, 18):
        cv2.line(image, (x, 1280), (x, 1370), (0, 0, 0), 5 if x % 3 else 9)
    encoded, buffer = cv2.imencode(".png", image)
    if not encoded:
        raise RuntimeError("Could not encode the generated CONFIG label.")
    return buffer.tobytes()


class ConfigOCRApiUploadTests(unittest.TestCase):
    def test_config_upload_returns_only_the_two_targeted_fields(self) -> None:
        configured_fields = [
            {"field_name": "MODEL_NO", "display_name": "Model No."},
            {"field_name": "QUEUE_NO", "display_name": "Queue No."},
            {"field_name": "PROCESSOR", "display_name": "Processor"},
        ]
        app = FastAPI()
        app.include_router(router)
        provider = _ConfigUploadProvider()
        image = np.full((1000, 1000, 3), 255, dtype=np.uint8)
        encoded, buffer = cv2.imencode(".png", image)
        self.assertTrue(encoded)
        with tempfile.TemporaryDirectory() as temporary:
            service = OCRService(provider, OCRArtifactStore(Path(temporary)))
            with patch("app.api.routes.ocr_service", service), TestClient(app) as client:
                response = client.post(
                    "/ocr/extract",
                    files={"file": ("config.png", buffer.tobytes(), "image/png")},
                    data={"field_config": json.dumps(configured_fields)},
                )

        self.assertEqual(200, response.status_code, response.text)
        result = response.json()
        self.assertEqual(36, provider.calls)
        self.assertEqual(
            {"MODEL_NO", "QUEUE_NO"},
            {candidate["field_name"] for candidate in result["field_candidates"]},
        )
        self.assertEqual(
            ["HP Laptop 15-fd1654TU", "E52PWPA#ACJ"],
            [candidate["value"] for candidate in result["field_candidates"]],
        )
        self.assertIn("HP Laptop 15-fd1654TU", result["raw_text"])
        self.assertIn("E52PWPA#ACJ", result["raw_text"])


@unittest.skipUnless(_tesseract_available(), "Native Tesseract is required for this OCR API test.")
class OCRApiEndToEndTests(unittest.TestCase):
    def test_tesseract_upload_extracts_only_config_model_and_queue_rois(self) -> None:
        configured_fields = [
            {"field_name": "MODEL_NO", "display_name": "Model No.", "confidence_threshold": 0.55},
            {"field_name": "QUEUE_NO", "display_name": "Queue No.", "confidence_threshold": 0.55},
        ]
        app = FastAPI()
        app.include_router(router)
        original = _make_config_label()

        with tempfile.TemporaryDirectory() as temporary:
            service = OCRService(
                TesseractOCRProvider(),
                OCRArtifactStore(Path(temporary)),
            )
            with patch("app.api.routes.ocr_service", service), TestClient(app) as client:
                response = client.post(
                    "/ocr/extract",
                    files={"file": ("config.png", original, "image/png")},
                    data={"field_config": json.dumps(configured_fields)},
                )

        self.assertEqual(200, response.status_code, response.text)
        result = response.json()
        candidates = result["field_candidates"]
        self.assertEqual({"MODEL_NO", "QUEUE_NO"}, {candidate["field_name"] for candidate in candidates})
        self.assertEqual(
            {"MODEL_NO": "HP Laptop 15-fd1654TU", "QUEUE_NO": "E52PWPA#ACJ"},
            {candidate["field_name"]: candidate["value"] for candidate in candidates},
        )
        self.assertNotIn("Processor", result["raw_text"])
        self.assertNotIn("barcode", result["raw_text"].casefold())

    def test_extracts_configured_fields_and_persists_original_cleaned_txt_and_json(self) -> None:
        configured_fields = [
            {"field_name": "MODEL", "display_name": "Model", "aliases": ["Product Model"]},
            {"field_name": "PROCESSOR", "display_name": "Processor", "aliases": ["CPU"]},
        ]
        app = FastAPI()
        app.include_router(router)

        with tempfile.TemporaryDirectory() as temporary:
            service = OCRService(
                TesseractOCRProvider(),
                OCRArtifactStore(Path(temporary)),
            )
            responses = []
            with patch("app.api.routes.ocr_service", service), TestClient(app) as client:
                for role, model in (
                    ("MASTER_CO", "15-fd1654TU"),
                    ("ACTUAL_CO", "15-fd1652TU"),
                    ("MASTER_CONFIG", "15-fd1654TU"),
                    ("ACTUAL_CONFIG", "15-fd1689TU"),
                ):
                    original = _make_label(model)
                    response = client.post(
                        "/ocr/extract",
                        files={"file": (f"{role}.png", original, "image/png")},
                        data={
                            "field_config": json.dumps(configured_fields),
                            "minimum_confidence": "0.5",
                        },
                    )

                    self.assertEqual(200, response.status_code, response.text)
                    result = response.json()
                    responses.append(result)
                    self.assertEqual("Tesseract", result["engine"])
                    self.assertTrue(result["raw_text"].strip())
                    self.assertGreater(result["ocr_confidence"], 0)
                    self.assertEqual(
                        {"MODEL", "PROCESSOR"},
                        {candidate["field_name"] for candidate in result["field_candidates"]},
                    )

                    job_directory = Path(temporary, result["job_id"])
                    self.assertEqual(original, (job_directory / "original.png").read_bytes())
                    self.assertIsNotNone(cv2.imread(str(job_directory / "cleaned.png")))
                    self.assertEqual(
                        result["raw_text"],
                        (job_directory / "ocr.txt").read_text(encoding="utf-8"),
                    )
                    parsed = json.loads((job_directory / "parsed.json").read_text(encoding="utf-8"))
                    self.assertEqual(result["job_id"], parsed["job_id"])
                    self.assertEqual(result["raw_text"], parsed["raw_text"])

            model_candidates = {
                role: next(item for item in result["field_candidates"] if item["field_name"] == "MODEL")
                for role, result in zip(
                    ("MASTER_CO", "ACTUAL_CO", "MASTER_CONFIG", "ACTUAL_CONFIG"),
                    responses,
                    strict=True,
                )
            }
            self.assertIn("1654", model_candidates["MASTER_CO"]["value"])
            self.assertIn("1652", model_candidates["ACTUAL_CO"]["value"])
            self.assertIn("1689", model_candidates["ACTUAL_CONFIG"]["value"])
            self.assertEqual(
                ["Intel Core 5 120U"] * 4,
                [
                    next(item for item in result["field_candidates"] if item["field_name"] == "PROCESSOR")["value"]
                    for result in responses
                ],
            )


if __name__ == "__main__":
    unittest.main()
