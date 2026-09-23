"""Convert Open-SL's ordered regex substitutions into app profile JSON.

Run with the checked-out Open-SL repository path. The source package declares
MIT in package.json; see third_party/open-sl/NOTICE.md for provenance.
"""

import json
import pathlib
import re
import sys

SOURCE_NAMES = {
    "dl_manel": ("DL Manel", "dl_manel_to_unicode", "unicode_to_dl_manel", ["DL-Manel-bold", "DL-Araliya", "DL-Champika", "DL-Divani"]),
    "kaputa": ("kaputadotcom", "kaputa_to_unicode", "unicode_to_kaputa", ["Kaputa"]),
    "amalee": ("Amalee", "amalee_to_unicode", None, []),
    "thibus": ("Thibus Sinhala", "thibus_to_unicode", None, []),
}

REPLACEMENT = re.compile(
    r'^text = text\.replace\(/((?:\\/|[^/])*)/g,\s*("(?:\\"|[^"])*")\);$'
)


def steps(path: pathlib.Path) -> list[dict[str, str]]:
    result = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if "text.replace" not in line:
            continue
        match = REPLACEMENT.fullmatch(line)
        if not match:
            raise ValueError(f"Unrecognized replacement in {path.name}: {line}")
        pattern = normalize_js_regex(match.group(1))
        replacement = js_string(match.group(2))
        result.append({"pattern": pattern, "replacement": replacement})
    if not result:
        raise ValueError(f"No replacement steps found in {path}")
    return result


def normalize_js_regex(pattern: str) -> str:
    """Remove JS identity escapes that .NET rejects or interprets differently."""
    output = []
    i = 0
    preserved = set(r".^$*+?{}[]\|()bBdDsSwWfFnrtvuxc0")
    while i < len(pattern):
        if pattern[i] != "\\" or i + 1 == len(pattern):
            output.append(pattern[i])
            i += 1
        else:
            char = pattern[i + 1]
            if char in preserved:
                output.append("\\" + char)
            else:
                output.append(char)
            i += 2
    return "".join(output)


def js_string(quoted: str) -> str:
    """Decode JS string escapes, including identity escapes absent from JSON."""
    body = quoted[1:-1]
    output = []
    i = 0
    escapes = {"n": "\n", "r": "\r", "t": "\t", "b": "\b", "f": "\f", "v": "\v", "0": "\0"}
    while i < len(body):
        if body[i] != "\\":
            output.append(body[i])
            i += 1
            continue
        i += 1
        if i == len(body):
            raise ValueError("Trailing backslash in JS string")
        char = body[i]
        if char == "u" and i + 4 < len(body) and re.fullmatch(r"[0-9a-fA-F]{4}", body[i + 1:i + 5]):
            output.append(chr(int(body[i + 1:i + 5], 16)))
            i += 5
        elif char == "x" and i + 2 < len(body) and re.fullmatch(r"[0-9a-fA-F]{2}", body[i + 1:i + 3]):
            output.append(chr(int(body[i + 1:i + 3], 16)))
            i += 3
        else:
            output.append(escapes.get(char, char))
            i += 1
    return "".join(output)


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: import_open_sl.py OPEN_SL_REPO OUTPUT_DIRECTORY")
    source = pathlib.Path(sys.argv[1]) / "src" / "translators"
    output = pathlib.Path(sys.argv[2])
    output.mkdir(parents=True, exist_ok=True)
    for profile_id, (font, decoder, encoder, aliases) in SOURCE_NAMES.items():
        profile = {
            "id": profile_id,
            "fontName": font,
            "aliases": aliases,
            "source": "https://github.com/Open-SL/sinhala-unicode-converter",
            "decodeSteps": steps(source / f"{decoder}.ts"),
        }
        if encoder:
            profile["encodeSteps"] = steps(source / f"{encoder}.ts")
        destination = output / f"{profile_id}.json"
        destination.write_text(json.dumps(profile, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(profile_id, len(profile["decodeSteps"]), len(profile.get("encodeSteps", [])))


if __name__ == "__main__":
    main()
