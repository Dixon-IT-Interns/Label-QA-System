import re
import unicodedata
from dataclasses import dataclass

from app.schemas.ocr import ConfiguredField, FieldCandidate, OCRLine


@dataclass(frozen=True)
class _Geometry:
    left: float
    right: float
    top: float
    bottom: float

    @property
    def height(self) -> float:
        return max(1.0, self.bottom - self.top)

    @property
    def center_y(self) -> float:
        return (self.top + self.bottom) / 2


@dataclass(frozen=True)
class _LabelMatch:
    field_name: str
    label: str
    value: str | None
    line_index: int
    ambiguous: bool = False


def match_configured_fields(
    fields: list[ConfiguredField], lines: list[OCRLine]
) -> list[FieldCandidate]:
    labels = {
        field.field_name: sorted(
            {
                _normalize(field.field_name),
                _normalize(field.display_name),
                *(_normalize(alias) for alias in field.aliases),
            },
            key=len,
            reverse=True,
        )
        for field in fields
    }
    labels = {
        field_name: [label for label in values if label]
        for field_name, values in labels.items()
    }
    all_labels = sorted(
        {(field_name, label) for field_name, values in labels.items() for label in values},
        key=lambda item: len(item[1]),
        reverse=True,
    )
    line_matches = [
        _match_line(line.text, all_labels, index)
        for index, line in enumerate(lines)
    ]
    candidates = []

    for field in fields:
        field_matches = [
            match for line_matches_for_line in line_matches
            for match in line_matches_for_line
            if match.field_name == field.field_name
        ]
        values: dict[tuple[str, int], tuple[str, float, list[list[float]] | None]] = {}
        label_seen = bool(field_matches)
        colliding_label = any(match.ambiguous for match in field_matches)
        inferred_value = False

        for match in field_matches:
            if match.ambiguous:
                continue
            if match.value:
                value, confidence, box = _with_continuation(
                    match.value, match.line_index, lines, line_matches
                )
                value = _clean_field_value(field, value)
                values[(value, match.line_index)] = (
                    value,
                    confidence,
                    box or lines[match.line_index].box,
                )
                continue

            related = _find_related_value(match.line_index, lines, line_matches)
            for value_index in related:
                value, confidence, box = _with_continuation(
                    lines[value_index].text.strip(),
                    value_index,
                    lines,
                    line_matches,
                )
                value = _clean_field_value(field, value)
                confidence = min(confidence, lines[match.line_index].confidence)
                values[(value, value_index)] = (value, confidence, box)

        if not field_matches:
            for index, line in enumerate(lines):
                value = _infer_specification_value(field, line.text)
                if value:
                    values[(value, index)] = (value, line.confidence, line.box)
                    inferred_value = True

        unique_values = {value for value, _, _ in values.values()}
        if len(unique_values) == 1:
            value, confidence, box = next(iter(values.values()))
            candidates.append(
                FieldCandidate(
                    field_name=field.field_name,
                    value=value,
                    confidence=confidence,
                    box=box,
                    status="DETECTED",
                    reason=(
                        "A configured label and value were associated from OCR text and layout."
                        if not inferred_value
                        else "A configured laptop specification pattern was detected in OCR text."
                    ),
                )
            )
        elif len(unique_values) > 1:
            candidates.append(
                FieldCandidate(
                    field_name=field.field_name,
                    confidence=min(confidence for _, confidence, _ in values.values()),
                    status="AMBIGUOUS",
                    reason="Multiple distinct OCR values matched this configured field.",
                )
            )
        elif colliding_label:
            candidates.append(
            FieldCandidate(
                field_name=field.field_name,
                confidence=0,
                status="AMBIGUOUS",
                reason="A configured label is shared by multiple fields in this profile.",
            )
            )
        else:
            candidates.append(
                FieldCandidate(
                    field_name=field.field_name,
                    confidence=0,
                    status="AMBIGUOUS" if label_seen else "NOT_FOUND",
                    reason=(
                        "The configured label was detected, but its value could not be safely associated."
                        if label_seen
                        else "No configured field label was detected."
                    ),
                )
            )

    return candidates


def _infer_specification_value(field: ConfiguredField, text: str) -> str | None:
    kind = _field_kind(field)
    if kind is None:
        return None

    patterns = {
        "model": re.compile(
            r"\b(?:(?P<prefix>(?:HP\s+)?Laptop)\s+)?"
            r"(?P<number>\d{2})[^A-Z0-9]{0,4}"
            r"(?P<suffix>[A-Z]{2}\d{4,}[A-Z0-9]{0,8})\b",
            re.IGNORECASE,
        ),
        "processor": re.compile(
            r"\b(?:Intel|AMD)\b.{0,12}\b(?:Core|Ultra|Ryzen|Celeron|Pentium)\b"
            r".{0,30}?\b\d{2,5}[A-Z]{0,3}\b(?:\s+Processor)?",
            re.IGNORECASE,
        ),
        "memory": re.compile(
            r"\b\d+(?:\.\d+)?\s*GB\b.{0,40}?\b(?:SODIMM|DDR[345X]?|SDRAM|RAM)\b"
            r".{0,20}",
            re.IGNORECASE,
        ),
        "storage": re.compile(
            r"\b\d+(?:\.\d+)?\s*(?:GB|TB)\b.{0,40}?\b"
            r"(?:SSD|Solid State Drive|NVMe|HDD|Hard Disk(?: Drive)?|eMMC)\b",
            re.IGNORECASE,
        ),
        "operating_system": re.compile(
            r"\bWindows\s*\d{1,2}(?:\s+(?:Home|Pro|Enterprise|Education))?\b",
            re.IGNORECASE,
        ),
        "display": re.compile(
            r"\b(?:\d{1,2}(?:\.\d)?\s*(?:\"|inches?\b)?\s*)?"
            r"(?:FHD|WUXGA|UHD|OLED|QHD)\b"
            r"(?:\s+(?:LED|OLED)\b)?"
            r"(?:\s+Display\b)?"
            r"(?:\s*\(\s*\d{2,4}\s*nits\s*\))?",
            re.IGNORECASE,
        ),
        "battery": re.compile(
            r"\b(?:\d+\s*-\s*(?:cell|call|cel1)\s*[,.:]?\s*)?"
            r"\d+(?:[.,]\s*\d*)?\s*[-.]?\s*Wh\b",
            re.IGNORECASE,
        ),
    }
    match = patterns[kind].search(text)
    if match is None:
        return None

    if kind == "model":
        prefix = match.group("prefix")
        model = f"{match.group('number')}-{match.group('suffix')}"
        value = f"{'HP Laptop' if prefix and prefix.casefold().startswith('hp') else 'Laptop'} {model}" if prefix else model
        return _clean_field_value(field, value)

    return _clean_field_value(field, match.group(0)) or None


def _clean_field_value(field: ConfiguredField, value: str) -> str:
    cleaned = unicodedata.normalize("NFKC", value.replace("®", "").replace("™", ""))
    cleaned = re.sub(r"^[\s•●▪◦·@©¢\\|]+", "", cleaned)
    if _field_kind(field) == "model":
        model_match = re.search(
            r"\b(?:(?P<prefix>(?:HP\s+)?Laptop)\s+)?"
            r"(?P<number>\d{2})[^A-Z0-9]{0,4}"
            r"(?P<suffix>[A-Z]{2}\d{4,}[A-Z0-9]{0,8})\b",
            cleaned,
            flags=re.IGNORECASE,
        )
        if model_match is not None:
            prefix = model_match.group("prefix")
            model = f"{model_match.group('number')}-{model_match.group('suffix')}"
            cleaned = (
                f"{'HP Laptop' if prefix and prefix.casefold().startswith('hp') else 'Laptop'} {model}"
                if prefix
                else model
            )
    if _field_kind(field) == "battery" and re.search(r"\bWh\b", cleaned, re.IGNORECASE):
        cleaned = re.sub(
            r"\b(?P<count>\d+)\s*-\s*(?:cell|call|cel1)\s*[,.:]?\s*",
            r"\g<count>-cell, ",
            cleaned,
            flags=re.IGNORECASE,
        )
        cleaned = re.sub(r"(?<=\d)\s*[.,]\s*(?=Wh\b)", " ", cleaned, flags=re.IGNORECASE)
        cleaned = re.sub(r"\bWh\b[-.:,]+", "Wh", cleaned, flags=re.IGNORECASE)
    return " ".join(cleaned.split())


def _field_kind(field: ConfiguredField) -> str | None:
    names = {
        re.sub(r"[^a-z0-9]", "", name.casefold())
        for name in (field.field_name, field.display_name, *field.aliases)
    }
    if any("model" in name for name in names):
        return "model"
    if any("processor" in name or name == "cpu" for name in names):
        return "processor"
    if any("storage" in name or name in {"ssd", "harddrive"} for name in names):
        return "storage"
    if any("memory" in name or "ram" in name for name in names):
        return "memory"
    if any("operatingsystem" in name or name == "os" for name in names):
        return "operating_system"
    if any("display" in name or "screen" in name for name in names):
        return "display"
    if any("battery" in name for name in names):
        return "battery"
    return None


def _match_line(
    text: str,
    labels: list[tuple[str, str]],
    line_index: int,
) -> list[_LabelMatch]:
    normalized_text = _normalize(text)
    if not normalized_text:
        return []

    matches = []
    for field_name, label in labels:
        escaped = re.escape(label)
        value_match = re.match(
            rf"^\s*{escaped}(?:\s*[:=]\s*|\s+)(?P<value>.+?)\s*$",
            normalized_text,
            flags=re.IGNORECASE,
        )
        if value_match:
            matches.append(
                _LabelMatch(field_name, label, value_match.group("value").strip(), line_index)
            )
            continue

        if re.fullmatch(rf"\s*{escaped}\s*[:=]?\s*", normalized_text, flags=re.IGNORECASE):
            matches.append(_LabelMatch(field_name, label, None, line_index))

    if not matches:
        return []

    longest = max(len(match.label) for match in matches)
    longest_matches = [
        match for match in matches
        if len(match.label) == longest
    ]
    collides = len({match.field_name for match in longest_matches}) > 1
    return [
        _LabelMatch(
            match.field_name,
            match.label,
            None if collides else match.value,
            match.line_index,
            collides,
        )
        for match in longest_matches
    ]


def _find_related_value(
    label_index: int,
    lines: list[OCRLine],
    line_matches: list[list[_LabelMatch]],
) -> list[int]:
    label = lines[label_index]
    label_geometry = _geometry(label.box)
    if label_geometry is None:
        return []

    ranked: list[tuple[float, int]] = []
    for index, line in enumerate(lines):
        if index == label_index or not line.text.strip() or line_matches[index]:
            continue
        geometry = _geometry(line.box)
        if geometry is None:
            continue
        scale = max(label_geometry.height, geometry.height)
        baseline_delta = abs(label_geometry.center_y - geometry.center_y)
        horizontal_gap = geometry.left - label_geometry.right

        if baseline_delta <= max(12.0, scale * 0.8) and -scale * 0.2 <= horizontal_gap <= scale * 8:
            ranked.append((max(0.0, horizontal_gap), index))
            continue

        vertical_gap = geometry.top - label_geometry.bottom
        horizontal_alignment = abs(geometry.left - label_geometry.left)
        if (
            -scale * 0.2 <= vertical_gap <= max(24.0, scale * 2.5)
            and horizontal_alignment <= max(scale * 2.5, (label_geometry.right - label_geometry.left) * 0.25)
        ):
            ranked.append((scale + max(0.0, vertical_gap), index))

    if not ranked:
        return []

    ranked.sort()
    best_distance = ranked[0][0]
    tolerance = max(8.0, best_distance * 0.15)
    return [index for distance, index in ranked if distance <= best_distance + tolerance]


def _with_continuation(
    value: str,
    line_index: int,
    lines: list[OCRLine],
    line_matches: list[list[_LabelMatch]],
) -> tuple[str, float, list[list[float]] | None]:
    line = lines[line_index]
    value_parts = [value]
    confidence = line.confidence
    first_geometry = _geometry(line.box)
    previous_geometry = first_geometry

    if first_geometry is not None:
        for index in range(line_index + 1, min(len(lines), line_index + 3)):
            continuation = lines[index]
            geometry = _geometry(continuation.box)
            if (
                geometry is None
                or line_matches[index]
                or not continuation.text.strip()
                or _looks_like_label_value(continuation.text)
                or previous_geometry is None
            ):
                break
            vertical_gap = geometry.top - previous_geometry.bottom
            if (
                vertical_gap < -geometry.height * 0.2
                or vertical_gap > max(18.0, previous_geometry.height * 1.5)
                or abs(geometry.left - first_geometry.left) > max(16.0, first_geometry.height * 1.5)
            ):
                break
            value_parts.append(continuation.text.strip())
            confidence = min(confidence, continuation.confidence)
            previous_geometry = geometry

    return " ".join(value_parts).strip(), confidence, line.box


def _geometry(box: list[list[float]] | None) -> _Geometry | None:
    if not box:
        return None
    try:
        points = [(float(point[0]), float(point[1])) for point in box if len(point) >= 2]
    except (TypeError, ValueError, IndexError):
        return None
    if len(points) < 2:
        return None
    return _Geometry(
        left=min(point[0] for point in points),
        right=max(point[0] for point in points),
        top=min(point[1] for point in points),
        bottom=max(point[1] for point in points),
    )


def _normalize(value: str) -> str:
    return " ".join(unicodedata.normalize("NFKC", value).split())


def _looks_like_label_value(value: str) -> bool:
    return re.match(r"^[A-Za-z][A-Za-z0-9 /#()_.-]{1,39}\s*[:=]\s*\S", value.strip()) is not None
