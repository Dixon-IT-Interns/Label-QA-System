import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import cv2
import numpy as np

import pytesseract

from app.ocr.provider import OCRProvider, OCRResult, OCRUnavailableError
from app.ocr.tesseract_provider import TesseractOCRProvider
from app.schemas.ocr import ConfiguredField, FieldCandidate, ImageQuality, OCRLine
from app.image_processing.annotations import prepare_for_ocr
from app.services.artifact_storage import OCRArtifactStore
from app.services.field_matching import match_configured_fields
from app.services.ocr_service import (
    OCRService,
    _cross_check_serial_with_qr,
    _extract_co_labeled_value,
    _extract_co_qr,
    _serial_number_from_qr_payload,
)
from app.services.queue_number import (
    _Barcode,
    _locate_linear_barcode_region,
    _queue_roi,
    _select_candidate,
)


class ConfiguredFieldMatchingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.field = ConfiguredField(
            field_name="SERIAL_NUMBER",
            display_name="Serial Number",
            aliases=["S/N"],
        )

    def test_extracts_delimited_value_without_confusing_o_and_zero(self) -> None:
        result = match_configured_fields(
            [self.field],
            [OCRLine(text="S/N: O1-234", confidence=0.97)],
        )[0]

        self.assertEqual("DETECTED", result.status)
        self.assertEqual("O1-234", result.value)
        self.assertEqual(0.97, result.confidence)

    def test_extracts_adjacent_column_value(self) -> None:
        result = match_configured_fields(
            [self.field],
            [
                OCRLine(
                    text="Serial Number",
                    confidence=0.98,
                    box=[[10, 10], [115, 10], [115, 32], [10, 32]],
                ),
                OCRLine(
                    text="ABC123456",
                    confidence=0.94,
                    box=[[130, 10], [260, 10], [260, 32], [130, 32]],
                ),
            ],
        )[0]

        self.assertEqual("DETECTED", result.status)
        self.assertEqual("ABC123456", result.value)
        self.assertEqual(0.94, result.confidence)

    def test_extracts_multiline_value_below_label(self) -> None:
        result = match_configured_fields(
            [self.field],
            [
                OCRLine(
                    text="Serial Number",
                    confidence=0.99,
                    box=[[20, 10], [180, 10], [180, 32], [20, 32]],
                ),
                OCRLine(
                    text="ABC123",
                    confidence=0.96,
                    box=[[20, 38], [110, 38], [110, 60], [20, 60]],
                ),
                OCRLine(
                    text="XYZ789",
                    confidence=0.95,
                    box=[[20, 65], [110, 65], [110, 87], [20, 87]],
                ),
            ],
        )[0]

        self.assertEqual("DETECTED", result.status)
        self.assertEqual("ABC123 XYZ789", result.value)
        self.assertEqual(0.95, result.confidence)

    def test_does_not_append_a_distant_unrelated_line(self) -> None:
        result = match_configured_fields(
            [self.field],
            [
                OCRLine(
                    text="Serial Number: ABC123",
                    confidence=0.99,
                    box=[[20, 10], [240, 10], [240, 54], [20, 54]],
                ),
                OCRLine(
                    text="Unrelated label",
                    confidence=0.98,
                    box=[[20, 130], [220, 130], [220, 174], [20, 174]],
                ),
            ],
        )[0]

        self.assertEqual("ABC123", result.value)

    def test_ambiguous_values_require_review(self) -> None:
        result = match_configured_fields(
            [self.field],
            [
                OCRLine(
                    text="Serial Number",
                    confidence=0.99,
                    box=[[10, 10], [150, 10], [150, 32], [10, 32]],
                ),
                OCRLine(
                    text="ABC123",
                    confidence=0.96,
                    box=[[170, 10], [250, 10], [250, 32], [170, 32]],
                ),
                OCRLine(
                    text="XYZ789",
                    confidence=0.95,
                    box=[[174, 10], [260, 10], [260, 32], [174, 32]],
                ),
            ],
        )[0]

        self.assertEqual("AMBIGUOUS", result.status)
        self.assertIsNone(result.value)

    def test_missing_field_is_explicit(self) -> None:
        result = match_configured_fields(
            [self.field],
            [OCRLine(text="Model: 15-FD1652TU", confidence=0.98)],
        )[0]

        self.assertEqual("NOT_FOUND", result.status)
        self.assertEqual(0, result.confidence)

    def test_extracts_only_configured_unlabeled_laptop_specification_patterns(self) -> None:
        fields = [
            ConfiguredField(field_name="MODEL", display_name="Model"),
            ConfiguredField(field_name="PROCESSOR", display_name="Processor"),
            ConfiguredField(field_name="MEMORY", display_name="Memory"),
            ConfiguredField(field_name="STORAGE", display_name="Storage"),
            ConfiguredField(field_name="DISPLAY", display_name="Display"),
            ConfiguredField(field_name="BATTERY", display_name="Battery"),
        ]
        lines = [
            OCRLine(text="HP 15-fd1652TU", confidence=0.8),
            OCRLine(text="@ Intel® Core® 5 120U", confidence=0.7),
            OCRLine(text="8GB DDR4 SDRAM", confidence=0.75),
            OCRLine(text="512GB Solid State Drive", confidence=0.9),
            OCRLine(text='15.6" FHD LED display', confidence=0.85),
            OCRLine(text="41 Wh", confidence=0.8),
        ]

        candidates = match_configured_fields(fields, lines)

        self.assertEqual(["DETECTED"] * len(fields), [candidate.status for candidate in candidates])
        self.assertEqual(
            ["15-fd1652TU", "Intel Core 5 120U", "8GB DDR4 SDRAM",
             "512GB Solid State Drive", '15.6" FHD LED display', "41 Wh"],
            [candidate.value for candidate in candidates],
        )
        self.assertTrue(all("pattern" in candidate.reason for candidate in candidates))

    def test_unlabeled_specification_patterns_do_not_extract_unconfigured_fields(self) -> None:
        candidates = match_configured_fields(
            [ConfiguredField(field_name="MODEL", display_name="Model")],
            [OCRLine(text="Intel® Core® 5 120U", confidence=0.9)],
        )

        self.assertEqual("NOT_FOUND", candidates[0].status)

    def test_extracts_only_active_hp_product_fields_and_ignores_noise(self) -> None:
        fields = _hp_fields()
        lines = [
            OCRLine(text="HP Laptop 15-fd1654TU", confidence=0.98),
            OCRLine(text="Intel® Core™ 5 120U Processor", confidence=0.96),
            OCRLine(text="512GB Solid State Drive", confidence=0.94),
            OCRLine(text="8GB SODIMM DDR4 SDRAM", confidence=0.92),
            OCRLine(text="Windows 11", confidence=0.95),
            OCRLine(text='15.6" FHD LED Display (250 nits)', confidence=0.91),
            OCRLine(text="3-call, 41 Wh", confidence=0.90),
            OCRLine(text="For the disclaimer, visit HP.com", confidence=0.04),
            OCRLine(text="Y28% @ ©", confidence=0.01),
            OCRLine(text="Energy Star", confidence=0.02),
            OCRLine(text="QR barcode: 1234567890", confidence=0.99),
            OCRLine(text="Handwritten annotation: 7", confidence=0.99),
        ]

        candidates = match_configured_fields(fields, lines)

        self.assertEqual(["DETECTED"] * 7, [candidate.status for candidate in candidates])
        self.assertEqual(
            [
                "HP Laptop 15-fd1654TU",
                "Intel Core 5 120U Processor",
                "512GB Solid State Drive",
                "8GB SODIMM DDR4 SDRAM",
                "Windows 11",
                '15.6" FHD LED Display (250 nits)',
                "3-cell, 41 Wh",
            ],
            [candidate.value for candidate in candidates],
        )
        self.assertNotIn("QR", [field.display_name for field in fields])
        self.assertNotIn("Handwritten", [candidate.value for candidate in candidates])
        self.assertGreater(min(candidate.confidence for candidate in candidates), 0.8)

    def test_missing_active_specification_field_is_not_filled_by_unrelated_lines(self) -> None:
        battery = ConfiguredField(field_name="BATTERY", display_name="Battery")

        result = match_configured_fields(
            [battery],
            [
                OCRLine(text="For disclaimer and support visit HP.com", confidence=0.01),
                OCRLine(text="QR barcode 1234567890", confidence=0.99),
                OCRLine(text="Handwritten annotation 3", confidence=0.99),
            ],
        )[0]

        self.assertEqual("NOT_FOUND", result.status)
        self.assertIsNone(result.value)

    def test_corrects_call_to_cell_only_for_battery_context(self) -> None:
        fields = [
            ConfiguredField(field_name="BATTERY", display_name="Battery"),
            ConfiguredField(field_name="MODEL", display_name="Model"),
        ]

        candidates = match_configured_fields(
            fields,
            [
                OCRLine(text="3-call, 41 Wh", confidence=0.9),
                OCRLine(text="3-call", confidence=0.9),
            ],
        )

        self.assertEqual("3-cell, 41 Wh", candidates[0].value)
        self.assertEqual("NOT_FOUND", candidates[1].status)

    def test_extracts_battery_when_ocr_adds_predictable_punctuation(self) -> None:
        candidate = match_configured_fields(
            [ConfiguredField(field_name="BATTERY", display_name="Battery")],
            [OCRLine(text="� 3-cell. 41. Wh-", confidence=0.9)],
        )[0]

        self.assertEqual("DETECTED", candidate.status)
        self.assertEqual("3-cell, 41 Wh", candidate.value)

    def test_shared_field_alias_is_ambiguous_for_every_matching_field(self) -> None:
        duplicate = ConfiguredField(
            field_name="PRODUCT_SERIAL",
            display_name="Serial Number",
            aliases=[],
        )
        candidates = match_configured_fields(
            [self.field, duplicate],
            [OCRLine(text="Serial Number: ABC123", confidence=0.99)],
        )

        self.assertEqual(["AMBIGUOUS", "AMBIGUOUS"], [candidate.status for candidate in candidates])


class _FixedProvider(OCRProvider):
    def __init__(self, lines: list[OCRLine]) -> None:
        self.lines = lines

    @property
    def name(self) -> str:
        return "test-provider"

    def extract(self, image: np.ndarray) -> OCRResult:
        return OCRResult(
            version="test",
            raw_text="\n".join(line.text for line in self.lines),
            lines=self.lines,
        )


class _SequencedProvider(OCRProvider):
    def __init__(self, lines: list[list[OCRLine]]) -> None:
        self._lines = lines
        self.calls = 0

    @property
    def name(self) -> str:
        return "sequenced-test-provider"

    def extract(self, image: np.ndarray) -> OCRResult:
        lines = self._lines[min(self.calls, len(self._lines) - 1)]
        self.calls += 1
        return OCRResult(
            version="test",
            raw_text="\n".join(line.text for line in lines),
            lines=lines,
        )


class _ConfigROIProvider(OCRProvider):
    def __init__(self, model_lines: list[OCRLine], queue_lines: list[OCRLine]) -> None:
        self._results = [model_lines, queue_lines]
        self.shapes: list[tuple[int, int]] = []

    @property
    def name(self) -> str:
        return "config-roi-test-provider"

    def extract(self, image: np.ndarray) -> OCRResult:
        self.shapes.append((image.shape[0], image.shape[1]))
        lines = self._results[0 if len(self.shapes) <= 18 else 1]
        return OCRResult(
            version="test",
            raw_text="\n".join(line.text for line in lines),
            lines=lines,
        )


class _ConfigQueueVotesProvider(OCRProvider):
    def __init__(self, queue_texts: list[str]) -> None:
        self._queue_texts = queue_texts
        self.calls = 0

    @property
    def name(self) -> str:
        return "queue-votes-test-provider"

    def extract(self, image: np.ndarray) -> OCRResult:
        if self.calls < 18:
            text = "HP Laptop 15-fd1654TU"
        else:
            queue_call = self.calls - 18
            text = self._queue_texts[min(queue_call, len(self._queue_texts) - 1)]
        self.calls += 1
        line = OCRLine(text=text, confidence=0.52 if text.startswith("ES") else 0.92)
        return OCRResult(version="test", raw_text=text, lines=[line])


class TesseractProviderTests(unittest.TestCase):
    def test_preserves_raw_output_and_returns_word_confidence_and_line_box(self) -> None:
        data = {
            "text": ["S/N:", "ABC123"],
            "conf": ["90", "80"],
            "page_num": ["1", "1"],
            "block_num": ["1", "1"],
            "par_num": ["1", "1"],
            "line_num": ["1", "1"],
            "left": ["10", "50"],
            "top": ["20", "20"],
            "width": ["35", "40"],
            "height": ["20", "20"],
        }
        with (
            patch("pytesseract.image_to_string", return_value="S/N: ABC123\n"),
            patch("pytesseract.image_to_data", return_value=data),
            patch("pytesseract.get_tesseract_version", return_value="5.4.0"),
        ):
            result = TesseractOCRProvider().extract(np.zeros((40, 100), dtype=np.uint8))

        self.assertEqual("S/N: ABC123\n", result.raw_text)
        self.assertEqual("5.4.0", result.version)
        self.assertAlmostEqual(0.85, result.lines[0].confidence)
        self.assertEqual("S/N: ABC123", result.lines[0].text)
        self.assertEqual(
            [[10.0, 20.0], [90.0, 20.0], [90.0, 40.0], [10.0, 40.0]],
            result.lines[0].box,
        )

    def test_missing_tesseract_binary_reports_service_unavailable(self) -> None:
        error = pytesseract.pytesseract.TesseractNotFoundError()
        with patch("pytesseract.image_to_string", side_effect=error):
            with self.assertRaises(OCRUnavailableError):
                TesseractOCRProvider().extract(np.zeros((40, 100), dtype=np.uint8))


class OCRServicePolicyTests(unittest.TestCase):
    def test_extracts_serial_number_from_hp_qr_payload(self) -> None:
        self.assertEqual(
            "1N4635067J",
            _serial_number_from_qr_payload(
                "https;//hpsmart.com/QRC/I0B/01/885981339727/21/1N4635067J"
                "?240=E52PWPA%23ACJ&422=156"
            ),
        )
        self.assertIsNone(_serial_number_from_qr_payload("https://example.test/label/1N4635067J"))

    def test_qr_cross_check_confirms_matching_printed_serial_number(self) -> None:
        candidate = FieldCandidate(
            field_name="SERIAL_NUMBER",
            value="1N4635067J",
            confidence=0.94,
            status="DETECTED",
            reason="A field-valid value was read beside the S/N label in its dedicated region.",
        )

        result = _cross_check_serial_with_qr(
            candidate,
            "https;//hpsmart.com/QRC/I0B/01/885981339727/21/1N4635067J?240=E52PWPA%23ACJ",
        )

        self.assertEqual("DETECTED", result.status)
        self.assertEqual("1N4635067J", result.value)
        self.assertIn("matched", result.reason)

    def test_phone_image_serial_ocr_is_resolved_by_qr_despite_brackets_and_j3_confusion(self) -> None:
        field = ConfiguredField(field_name="SERIAL_NUMBER", display_name="S/N")
        qr_payload = (
            "https;//hpsmart.com/QRC/I0B/01/885981339727/21/1N4635067J"
            "?240=E52PWPA%23ACJ"
        )
        provider = _FixedProvider([
            OCRLine(text="S/N [_1N46350673_] Pio", confidence=0.717),
        ])

        candidate, _ = _extract_co_labeled_value(
            field,
            np.full((1024, 768, 3), 255, dtype=np.uint8),
            provider,
            "serialnumber",
            qr_payload,
        )

        self.assertEqual("DETECTED", candidate.status)
        self.assertEqual("1N4635067J", candidate.value)
        self.assertEqual(0.717, candidate.confidence)
        self.assertIn("cross-checked", candidate.reason)

    def test_qr_decoder_uses_zxing_fallback_when_opencv_cannot_decode(self) -> None:
        payload = "https://example.test/label/1N4635067J?240=E52PWPA%23ACJ"
        qr = cv2.QRCodeEncoder_create().encode(payload)
        qr = cv2.resize(qr, (240, 240), interpolation=cv2.INTER_NEAREST)
        image = np.full((1000, 1000, 3), 255, dtype=np.uint8)
        image[30:270, 650:890] = cv2.cvtColor(qr, cv2.COLOR_GRAY2BGR)

        with patch("app.services.ocr_service.cv2.QRCodeDetector") as detector_factory:
            detector = detector_factory.return_value
            detector.detectAndDecode.return_value = ("", None, None)
            detector.detectAndDecodeMulti.return_value = (False, (), (), None)
            candidate = _extract_co_qr(
                ConfiguredField(field_name="QR_CODE", display_name="QR Code"),
                image,
            )

        self.assertEqual("DETECTED", candidate.status)
        self.assertEqual(payload, candidate.value)

    def test_qr_cross_check_requires_review_for_conflicting_serial_number(self) -> None:
        candidate = FieldCandidate(
            field_name="SERIAL_NUMBER",
            value="1N4635062D",
            confidence=0.94,
            status="DETECTED",
            reason="A field-valid value was read beside the S/N label in its dedicated region.",
        )

        result = _cross_check_serial_with_qr(
            candidate,
            "https;//hpsmart.com/QRC/I0B/01/885981339727/21/1N4635067J?240=E52PWPA%23ACJ",
        )

        self.assertEqual("AMBIGUOUS", result.status)
        self.assertIsNone(result.value)
        self.assertIn("does not match", result.reason)

    def test_qr_cross_check_requires_decoded_qr_serial_for_s_n(self) -> None:
        candidate = FieldCandidate(
            field_name="SERIAL_NUMBER",
            value="1N4635067J",
            confidence=0.94,
            status="DETECTED",
            reason="A field-valid value was read beside the S/N label in its dedicated region.",
        )

        result = _cross_check_serial_with_qr(candidate, None)

        self.assertEqual("AMBIGUOUS", result.status)
        self.assertIsNone(result.value)
        self.assertIn("could not be cross-checked", result.reason)

    def test_co_extraction_marks_serial_ambiguous_when_it_conflicts_with_qr(self) -> None:
        fields = [
            ConfiguredField(field_name="SERIAL_NUMBER", display_name="S/N"),
            ConfiguredField(field_name="PART_NUMBER", display_name="P/N"),
            ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
            ConfiguredField(field_name="QR_CODE", display_name="QR Code"),
        ]
        provider = _FixedProvider([
            OCRLine(text="HP Laptop 15-fd1654TU", confidence=0.99),
            OCRLine(text="S/N 1N4635062D", confidence=0.99),
            OCRLine(text="P/N E52PWPA#ACJ", confidence=0.99),
        ])
        qr_payload = (
            "https;//hpsmart.com/QRC/I0B/01/885981339727/21/1N4635067J"
            "?240=E52PWPA%23ACJ"
        )

        with patch("app.services.ocr_service.cv2.QRCodeDetector") as detector_factory:
            detector = detector_factory.return_value
            detector.detectAndDecode.return_value = (qr_payload, None, None)
            detector.detectAndDecodeMulti.return_value = (False, (), (), None)
            candidates, _ = OCRService(provider)._extract_co_fields(
                np.full((1000, 1200, 3), 255, dtype=np.uint8),
                fields,
            )

        serial_candidate = next(candidate for candidate in candidates if candidate.field_name == "SERIAL_NUMBER")
        self.assertEqual("AMBIGUOUS", serial_candidate.status)
        self.assertIsNone(serial_candidate.value)
        self.assertIn("does not match", serial_candidate.reason)

    def test_co_extraction_does_not_guess_values_from_unrelated_label_text(self) -> None:
        fields = [
            ConfiguredField(field_name="SERIAL_NUMBER", display_name="S/N"),
            ConfiguredField(field_name="PART_NUMBER", display_name="P/N"),
            ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
            ConfiguredField(field_name="QR_CODE", display_name="QR Code"),
        ]
        provider = _FixedProvider([
            OCRLine(text="PRDN Order 1N4635062D", confidence=0.99),
            OCRLine(text="Work Order 99887766", confidence=0.99),
            OCRLine(text="WLAN MAC 00:11:22:33:44:55", confidence=0.99),
            OCRLine(text="Intel Core 5 120U Processor", confidence=0.99),
        ])

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            fields,
            label_type="CO",
        )

        self.assertEqual(["NOT_FOUND"] * 4, [candidate.status for candidate in response.field_candidates])
        self.assertTrue(all(candidate.value is None for candidate in response.field_candidates))
        self.assertEqual("", response.raw_text)
        self.assertEqual([], response.lines)

    def test_co_extraction_hides_values_below_field_confidence_threshold(self) -> None:
        fields = [
            ConfiguredField(field_name="SERIAL_NUMBER", display_name="S/N"),
            ConfiguredField(field_name="PART_NUMBER", display_name="P/N"),
            ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
            ConfiguredField(field_name="QR_CODE", display_name="QR Code"),
        ]
        provider = _FixedProvider([
            OCRLine(text="S/N 1N4635062D", confidence=0.4),
            OCRLine(text="P/N E52PWPA#ACJ", confidence=0.4),
            OCRLine(text="HP Laptop 15-fd1652TU", confidence=0.4),
        ])

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            fields,
            minimum_confidence=0.85,
            label_type="CO",
        )

        self.assertEqual("REVIEW_REQUIRED", response.status)
        self.assertTrue(all(candidate.value is None for candidate in response.field_candidates))

    def test_config_ocr_uses_only_top_model_and_below_barcode_queue_rois(self) -> None:
        provider = _ConfigROIProvider(
            [OCRLine(text="HP Laptop 15-fd1654TU", confidence=0.99)],
            [OCRLine(text="E52PWPA#ACJ", confidence=0.99)],
        )
        fields = [
            ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
            ConfiguredField(field_name="QUEUE_NO", display_name="Queue No."),
            ConfiguredField(field_name="PROCESSOR", display_name="Processor"),
        ]

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            fields,
            minimum_confidence=0.85,
        )

        self.assertEqual(36, len(provider.shapes))
        self.assertTrue(all(shape == (186, 1200) for shape in provider.shapes[:18]))
        self.assertTrue(all(shape == (200, 1200) for shape in provider.shapes[18:]))
        self.assertEqual(["MODEL_NO", "QUEUE_NO"], [candidate.field_name for candidate in response.field_candidates])
        self.assertEqual(
            ["HP Laptop 15-fd1654TU", "E52PWPA#ACJ"],
            [candidate.value for candidate in response.field_candidates],
        )
        self.assertEqual("HP Laptop 15-fd1654TU\nE52PWPA#ACJ", response.raw_text)

    def test_config_roi_ignores_unrelated_low_confidence_ocr_noise(self) -> None:
        provider = _ConfigROIProvider(
            [
                OCRLine(text="HP Laptop 15-fd1654TU", confidence=0.99),
                OCRLine(text="Processor 120U", confidence=0.02),
            ],
            [
                OCRLine(text="E52PWPA # ACJ", confidence=0.96),
                OCRLine(text="Y28% @ ©", confidence=0.01),
            ],
        )

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            [
                ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
                ConfiguredField(field_name="QUEUE_NO", display_name="Queue No."),
            ],
            minimum_confidence=0.85,
        )

        self.assertEqual("DETECTED", response.field_candidates[0].status)
        self.assertEqual("DETECTED", response.field_candidates[1].status)
        self.assertEqual("E52PWPA#ACJ", response.field_candidates[1].value)
        self.assertGreater(response.field_candidates[1].confidence, 0.96)

    def test_config_roi_queue_value_is_extracted_without_barcode_decoding(self) -> None:
        provider = _ConfigROIProvider(
            [OCRLine(text="HP Laptop 15-fd1654TU", confidence=0.99)],
            [OCRLine(text="E52PWPA # ACJ", confidence=0.97)],
        )

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            [
                ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
                ConfiguredField(field_name="QUEUE_NO", display_name="Queue No."),
            ],
        )

        self.assertEqual("E52PWPA#ACJ", response.field_candidates[1].value)
        self.assertEqual(2, len(response.field_candidates))

    def test_config_queue_candidate_consensus_resolves_an_inserted_character(self) -> None:
        provider = _ConfigQueueVotesProvider(
            ["E52PWPA#ACJ"] * 8 + ["ES52PWPA#ACJ (a)"] * 10
        )

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            [
                ConfiguredField(field_name="MODEL_NO", display_name="Model No.", confidence_threshold=0.55),
                ConfiguredField(field_name="QUEUE_NO", display_name="Queue No.", confidence_threshold=0.55),
            ],
            minimum_confidence=0.85,
        )

        queue = response.field_candidates[1]
        self.assertEqual(36, provider.calls)
        self.assertEqual("DETECTED", queue.status)
        self.assertEqual("E52PWPA#ACJ", queue.value)
        self.assertEqual("ocr_consensus", queue.debug["selectionSource"])
        self.assertEqual(8, next(item["frequency"] for item in queue.debug["candidates"] if item["value"] == "E52PWPA#ACJ"))

    def test_config_queue_single_low_quality_read_requires_review(self) -> None:
        provider = _ConfigQueueVotesProvider(
            ["E52PWPA#ACJ"] + ["blurred text"] * 17
        )

        response = OCRService(provider).inspect(
            self._config_image_bytes(),
            [
                ConfiguredField(field_name="MODEL_NO", display_name="Model No."),
                ConfiguredField(field_name="QUEUE_NO", display_name="Queue No."),
            ],
        )

        queue = response.field_candidates[1]
        self.assertEqual("AMBIGUOUS", queue.status)
        self.assertIsNone(queue.value)
        self.assertEqual(18, queue.debug["ocrPasses"])

    def test_barcode_cross_validation_resolves_one_extra_ocr_character(self) -> None:
        candidate, confidence, source = _select_candidate(
            {
                "ES52PWPA#ACJ": {
                    "confidence": 0.52,
                    "passes": [{"variant": "otsu", "psm": 7}],
                }
            },
            "E52PWPA#ACJ",
        )

        self.assertEqual("E52PWPA#ACJ", candidate)
        self.assertEqual("barcode_cross_validated", source)
        self.assertGreater(confidence, 0.55)

    def test_barcode_box_places_queue_roi_immediately_below_the_bars(self) -> None:
        image = np.full((1000, 800, 3), 255, dtype=np.uint8)
        _, roi_box, roi_source = _queue_roi(
            image,
            _Barcode("E52PWPA#ACJ", "Code128", (100, 700, 500, 100)),
        )

        self.assertEqual((160, 803, 380, 50), roi_box)
        self.assertEqual("barcode_below_region", roi_source)

    def test_locates_queue_text_below_linear_barcode_when_decode_fails(self) -> None:
        image = np.full((1000, 800, 3), 255, dtype=np.uint8)
        for x in range(120, 680, 20):
            cv2.rectangle(image, (x, 720), (x + 8, 790), (0, 0, 0), -1)
        cv2.putText(
            image,
            "E52PWPA#ACJ",
            (235, 865),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.8,
            (0, 0, 0),
            2,
            cv2.LINE_AA,
        )

        barcode = _locate_linear_barcode_region(image)
        self.assertIsNotNone(barcode)
        assert barcode is not None
        self.assertIsNone(barcode.value)
        roi, roi_box, roi_source = _queue_roi(image, barcode)

        self.assertEqual("barcode_text_region", roi_source)
        self.assertLess(roi_box[2], barcode.box[2] if barcode.box else 0)
        self.assertLessEqual(roi.shape[0], 40)

    def test_retries_only_low_confidence_configured_fields_on_upscaled_image(self) -> None:
        provider = _SequencedProvider(
            [
                [OCRLine(text="512GB Solid State Drive", confidence=0.4)],
                [OCRLine(text="512 GB SSD", confidence=0.82)],
            ]
        )
        field = ConfiguredField(
            field_name="STORAGE",
            display_name="Storage",
            confidence_threshold=0.55,
        )

        response = OCRService(provider).inspect(
            self._image_bytes(),
            [field],
            minimum_confidence=0.85,
        )

        self.assertEqual(2, provider.calls)
        self.assertEqual("DETECTED", response.field_candidates[0].status)
        self.assertEqual("512 GB SSD", response.field_candidates[0].value)
        self.assertEqual(0.82, response.field_candidates[0].confidence)

    def test_retries_a_missing_configured_field_before_returning_not_found(self) -> None:
        provider = _SequencedProvider(
            [
                [OCRLine(text="For the disclaimer visit HP.com", confidence=0.01)],
                [OCRLine(text="512 GB SSD", confidence=0.82)],
            ]
        )
        field = ConfiguredField(
            field_name="STORAGE",
            display_name="Storage",
            confidence_threshold=0.55,
        )

        response = OCRService(provider).inspect(
            self._image_bytes(),
            [field],
            minimum_confidence=0.85,
        )

        self.assertEqual(2, provider.calls)
        self.assertEqual("DETECTED", response.field_candidates[0].status)
        self.assertEqual("512 GB SSD", response.field_candidates[0].value)

    def test_conflicting_original_and_retry_values_remain_ambiguous(self) -> None:
        provider = _SequencedProvider(
            [
                [OCRLine(text="512GB Solid State Drive", confidence=0.4)],
                [OCRLine(text="1TB Solid State Drive", confidence=0.82)],
            ]
        )
        field = ConfiguredField(
            field_name="STORAGE",
            display_name="Storage",
            confidence_threshold=0.55,
        )

        response = OCRService(provider).inspect(
            self._image_bytes(),
            [field],
            minimum_confidence=0.85,
        )

        self.assertEqual("AMBIGUOUS", response.field_candidates[0].status)
        self.assertIsNone(response.field_candidates[0].value)

    def test_low_confidence_field_is_warned_without_rejecting_other_image_evidence(self) -> None:
        image_bytes = self._image_bytes()
        line = OCRLine(text="Serial Number: ABC123", confidence=0.6)
        response = OCRService(_FixedProvider([line])).inspect(
            image_bytes,
            [ConfiguredField(field_name="SERIAL", display_name="Serial Number")],
            minimum_confidence=0.85,
        )

        self.assertEqual("PROCESSING", response.status)
        self.assertIn("below the confidence threshold", response.warnings[0])
        self.assertEqual(0.6, response.field_candidates[0].confidence)

    def test_blue_annotation_is_masked_without_requiring_image_level_review(self) -> None:
        image = np.full((480, 640, 3), 255, dtype=np.uint8)
        cv2.putText(
            image,
            "SERIAL NUMBER: ABC123",
            (30, 180),
            cv2.FONT_HERSHEY_SIMPLEX,
            1.0,
            (0, 0, 0),
            2,
            cv2.LINE_AA,
        )
        cv2.line(image, (35, 205), (430, 205), (255, 0, 0), 3)
        encoded, buffer = cv2.imencode(".png", image)
        self.assertTrue(encoded)
        response = OCRService(
            _FixedProvider([OCRLine(text="SERIAL NUMBER: ABC123", confidence=0.99)])
        ).inspect(buffer.tobytes())

        self.assertEqual("PROCESSING", response.status)
        self.assertTrue(any("masked from OCR preprocessing" in warning for warning in response.warnings))
        self.assertEqual((480, 640), (response.image_quality.height, response.image_quality.width))

    def test_blue_pixels_are_removed_from_ocr_input_but_source_image_is_unchanged(self) -> None:
        image = np.full((100, 200, 3), 255, dtype=np.uint8)
        cv2.line(image, (10, 50), (180, 50), (255, 0, 0), 3)
        original = image.copy()
        prepared = prepare_for_ocr(
            image,
            ImageQuality(width=200, height=100, blur_score=100, brightness_mean=255, contrast_stddev=20),
        )

        self.assertTrue(np.all(prepared[50, 20] == 255))
        np.testing.assert_array_equal(original, image)

    def test_low_contrast_preprocessing_preserves_source_dimensions_and_pixels(self) -> None:
        image = np.full((480, 640, 3), 115, dtype=np.uint8)
        cv2.putText(
            image,
            "SERIAL",
            (30, 180),
            cv2.FONT_HERSHEY_SIMPLEX,
            1.0,
            (100, 100, 100),
            2,
            cv2.LINE_AA,
        )
        original = image.copy()
        prepared = prepare_for_ocr(
            image,
            ImageQuality(
                width=640,
                height=480,
                blur_score=20,
                brightness_mean=115,
                contrast_stddev=5,
            ),
        )

        self.assertEqual(image.shape, prepared.shape)
        self.assertFalse(np.array_equal(original, prepared))
        np.testing.assert_array_equal(original, image)

    def test_preprocessing_variants_do_not_modify_the_source_image(self) -> None:
        image = np.full((480, 640, 3), 255, dtype=np.uint8)
        original = image.copy()
        quality = ImageQuality(
            width=640,
            height=480,
            blur_score=100,
            brightness_mean=255,
            contrast_stddev=0,
        )

        for method in ("original", "grayscale", "clahe", "otsu", "adaptive"):
            with self.subTest(method=method):
                processed = prepare_for_ocr(image, quality, method)
                self.assertEqual((480, 640), processed.shape[:2])
                self.assertFalse(np.shares_memory(image, processed))
                np.testing.assert_array_equal(original, image)

    def test_persists_original_cleaned_raw_and_parsed_artifacts(self) -> None:
        image_bytes = self._image_bytes()
        with tempfile.TemporaryDirectory() as temporary:
            response = OCRService(
                _FixedProvider([OCRLine(text="Serial Number: ABC123", confidence=0.98)]),
                OCRArtifactStore(Path(temporary)),
            ).inspect(
                image_bytes,
                [ConfiguredField(field_name="SERIAL", display_name="Serial Number")],
                original_extension=".png",
            )

            self.assertIsNotNone(response.job_id)
            self.assertIsNotNone(response.artifacts)
            references = response.artifacts
            original_path = Path(temporary, references.original_image)
            cleaned_path = Path(temporary, references.cleaned_image)
            raw_path = Path(temporary, references.raw_ocr_text)
            parsed_path = Path(temporary, references.parsed_json)
            self.assertEqual(image_bytes, original_path.read_bytes())
            self.assertIsNotNone(cv2.imread(str(cleaned_path)))
            self.assertEqual(response.raw_text, raw_path.read_text(encoding="utf-8"))
            self.assertEqual(response.model_dump(mode="json"), json.loads(parsed_path.read_text(encoding="utf-8")))
            np.testing.assert_array_equal(
                cv2.imdecode(np.frombuffer(image_bytes, dtype=np.uint8), cv2.IMREAD_COLOR),
                cv2.imdecode(np.frombuffer(image_bytes, dtype=np.uint8), cv2.IMREAD_COLOR),
            )

    @staticmethod
    def _image_bytes() -> bytes:
        image = np.full((480, 640, 3), 255, dtype=np.uint8)
        cv2.putText(
            image,
            "SERIAL NUMBER: ABC123",
            (30, 180),
            cv2.FONT_HERSHEY_SIMPLEX,
            1.0,
            (0, 0, 0),
            2,
            cv2.LINE_AA,
        )
        encoded, buffer = cv2.imencode(".png", image)
        if not encoded:
            raise RuntimeError("Could not encode the OCR test image.")
        return buffer.tobytes()

    @staticmethod
    def _config_image_bytes() -> bytes:
        image = np.full((1000, 1000, 3), 255, dtype=np.uint8)
        encoded, buffer = cv2.imencode(".png", image)
        if not encoded:
            raise RuntimeError("Could not encode the CONFIG ROI test image.")
        return buffer.tobytes()


def _hp_fields() -> list[ConfiguredField]:
    return [
        ConfiguredField(field_name="MODEL", display_name="Model"),
        ConfiguredField(field_name="PROCESSOR", display_name="Processor"),
        ConfiguredField(field_name="STORAGE", display_name="Storage"),
        ConfiguredField(field_name="RAM", display_name="RAM"),
        ConfiguredField(field_name="OPERATING_SYSTEM", display_name="Operating System"),
        ConfiguredField(field_name="DISPLAY", display_name="Display"),
        ConfiguredField(field_name="BATTERY", display_name="Battery"),
    ]


if __name__ == "__main__":
    unittest.main()
