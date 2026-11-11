import cv2
import numpy as np

from app.config import MIN_IMAGE_HEIGHT, MIN_IMAGE_WIDTH
from app.schemas.ocr import ImageQuality


class ImageQualityError(ValueError):
    def __init__(self, message: str, quality: ImageQuality) -> None:
        super().__init__(message)
        self.quality = quality


def decode_and_check_image(image_bytes: bytes) -> tuple[np.ndarray, ImageQuality]:
    encoded = np.frombuffer(image_bytes, dtype=np.uint8)
    image = cv2.imdecode(encoded, cv2.IMREAD_COLOR)
    if image is None:
        raise ValueError("The uploaded file is not a decodable image.")

    height, width = image.shape[:2]
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blur_score = float(cv2.Laplacian(gray, cv2.CV_64F).var())
    brightness_mean = float(np.mean(gray))
    contrast_stddev = float(np.std(gray))
    quality = ImageQuality(
        width=width,
        height=height,
        blur_score=round(blur_score, 2),
        brightness_mean=round(brightness_mean, 2),
        contrast_stddev=round(contrast_stddev, 2),
    )

    if width < MIN_IMAGE_WIDTH or height < MIN_IMAGE_HEIGHT:
        raise ImageQualityError(
            f"Image dimensions must be at least {MIN_IMAGE_WIDTH}x{MIN_IMAGE_HEIGHT} pixels.",
            quality,
        )
    return image, quality
