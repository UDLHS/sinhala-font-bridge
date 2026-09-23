"""Build a conservative ISI/Island profile from kawishkamd's MIT mapping.

The upstream table contains a whitespace-to-Sinhala mapping and a few
conflicting legacy keys. Exclude those keys rather than guessing.
"""

import collections
import json
import pathlib
import sys


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: import_isi.py DATA_LIST_JSON OUTPUT_JSON")
    rows = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
    by_legacy = collections.defaultdict(set)
    for row in rows:
        unicode_text, legacy_text = row.get("uni"), row.get("isi")
        if unicode_text and legacy_text and legacy_text.strip():
            by_legacy[legacy_text].add(unicode_text)
    unique = {}
    ambiguous = 0
    for legacy, outputs in by_legacy.items():
        if len(outputs) == 1:
            unique[legacy] = next(iter(outputs))
            continue
        sinhala = [value for value in outputs if any('\u0d80' <= c <= '\u0dff' for c in value)]
        if len(sinhala) == 1:
            # Prefer the Sinhala meaning when a Latin quote reuses its key.
            # Text containing that quote then fails round-trip validation.
            unique[legacy] = sinhala[0]
        else:
            ambiguous += 1
    profile = {
        "id": "isi_island",
        "fontName": "ISI Font",
        "aliases": ["ISIFont", "Island"],
        "source": "https://github.com/kawishkamd/sinhala-legacy-converter",
        "singles": {key: value for key, value in unique.items() if len(key) == 1},
        "combos": {key: value for key, value in unique.items() if len(key) > 1},
    }
    pathlib.Path(sys.argv[2]).write_text(json.dumps(profile, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("usable", len(unique), "ambiguous excluded", ambiguous)


if __name__ == "__main__":
    main()
