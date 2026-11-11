import json
from pathlib import Path
from uuid import uuid4

import cv2
import numpy as np

from app.schemas.ocr import OCRArtifactReferences, OCRResponse


class OCRArtifactStore:
    def __init__(self, root: Path) -> None:
        self._root = root.expanduser().resolve()

    def persist(
        self,
        original_bytes: bytes,
        original_extension: str,
        cleaned_image: np.ndarray,
        response: OCRResponse,
    ) -> OCRResponse:
        job_id = uuid4().hex
        extension = original_extension.lower()
        if extension not in {".jpg", ".jpeg", ".png", ".webp"}:
            extension = ".img"
        references = OCRArtifactReferences(
            original_image=f"{job_id}/original{extension}",
            cleaned_image=f"{job_id}/cleaned.png",
            raw_ocr_text=f"{job_id}/ocr.txt",
            parsed_json=f"{job_id}/parsed.json",
        )
        completed_response = response.model_copy(
            update={"job_id": job_id, "artifacts": references}
        )
        encoded, cleaned_bytes = cv2.imencode(".png", cleaned_image)
        if not encoded:
            raise OSError("Could not encode the cleaned image for OCR artifact storage.")

        self._root.mkdir(parents=True, exist_ok=True)
        destination = self._root / job_id
        destination.mkdir(parents=True, exist_ok=False)
        (destination / f"original{extension}").write_bytes(original_bytes)
        (destination / "cleaned.png").write_bytes(cleaned_bytes.tobytes())
        with (destination / "ocr.txt").open("w", encoding="utf-8", newline="") as stream:
            stream.write(response.raw_text)
        with (destination / "parsed.json").open("w", encoding="utf-8", newline="\n") as stream:
            json.dump(
                completed_response.model_dump(mode="json"),
                stream,
                ensure_ascii=False,
                indent=2,
            )
            stream.write("\n")
        return completed_response
