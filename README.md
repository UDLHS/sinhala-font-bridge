# Sinhala Font Bridge

**Sinhala typing and font conversion for Windows.**

Sinhala Font Bridge converts text between Unicode Sinhala and supported legacy font encodings, and provides KeyRep-style live typing with **Wijesekara keys** or **Singlish sounds**. It handles supported consonant and vowel-sign combinations, including ispili and papili, while you type into another desktop application.

Built with **C#**, **.NET 10**, and **Windows Forms**. Text processing runs locally; no account or cloud service is required.

> Font support depends on the encoding profile. This app does not automatically identify every Sinhala font, and compatibility with every desktop application is not guaranteed. See [supported encodings](#supported-encodings) and [limitations](#limitations).

## Contents

- [Features](#features)
- [Getting started](#getting-started)
- [Supported encodings](#supported-encodings)
- [Converting text](#converting-text)
- [Live typing](#live-typing)
- [Keyboard shortcuts and tray behavior](#keyboard-shortcuts-and-tray-behavior)
- [Using the result in Photoshop and other apps](#using-the-result-in-photoshop-and-other-apps)
- [Troubleshooting](#troubleshooting)
- [Privacy and local data](#privacy-and-local-data)
- [Building and testing](#building-and-testing)
- [Custom mapping profiles](#custom-mapping-profiles)
- [Project structure](#project-structure)
- [Limitations](#limitations)
- [Contributing](#contributing)
- [Credits and licensing](#credits-and-licensing)

## Features

- Convert supported legacy Sinhala text to Unicode, or Unicode to an encoding with an available encoder.
- Convert between supported legacy encodings through Unicode.
- Compose Sinhala from Wijesekara keys or Singlish input in the conversion panel.
- Apply KeyRep-style substitutions during live typing in an external text field.
- Show the legacy output alongside a readable Unicode preview.
- Detect Sinhala Unicode characters and use recognized font names in rich clipboard text as source hints.
- Verify legacy encoding by decoding the result back to Unicode; block copying when verification fails or Sinhala characters remain unmapped.
- Copy plain text and, when the font is installed, rich text with a font hint.
- Import additional JSON mapping profiles.
- Remember font, encoding, and typing-style selections.
- Stay in the Windows notification area; reopening the EXE restores the existing window instead of starting another copy.

## Getting started

### Requirements

- A Windows desktop environment. The documented portable package targets **Windows x64**.
- The desired Sinhala fonts installed separately, or available in the destination application.
- **.NET 10 SDK** to build from source. A self-contained portable build includes its runtime.

Font files are not included. A mapping profile supplies conversion rules; it does not install the corresponding font.

### Run from source

```powershell
git clone https://github.com/UDLHS/sinhala-font-bridge.git
cd sinhala-font-bridge
dotnet run --project SinhalaFontBridge/SinhalaFontBridge.csproj -c Release
```

### Run a portable build

The repository tracks source and mapping data. Generated EXEs and ZIPs are excluded from Git. To create a portable ZIP, see [publishing](#publish-a-portable-windows-package).

If you have a packaged ZIP:

1. Extract the **entire ZIP** into a normal folder. Do not run the EXE from inside the archive.
2. Keep `Profiles/`, `singlish_keys.json`, and `licenses/` beside `SinhalaFontBridge.exe`.
3. Run `SinhalaFontBridge.exe`.
4. Select the target encoding and matching font.

When upgrading, choose **Exit** from the old app's tray menu before starting the new version. The app currently uses a portable folder, not an installer, and does not configure startup with Windows.

## Supported encodings

| Encoding / profile | Legacy to Unicode | Unicode to legacy | Live typing | Recognized family names |
| --- | --- | --- | --- | --- |
| FM Abhaya | Yes | Yes | Yes | FM Abhaya, FM Bindumathi, FM Derana, FM Malithi |
| DL Manel | Yes | Yes | Yes | DL Manel, DL Araliya, DL Champika, DL Divani |
| Kaputa | Yes | Yes | Yes | kaputadotcom, Kaputa |
| ISI / Island | Yes | Yes | Yes | ISI Font, ISIFont, Island |
| Amalee | Yes | No | No | Amalee |
| Thibus Sinhala | Yes | No | No | Thibus Sinhala |

These are **six encoding profiles**, with aliases for related font families. Individual font variants still need visual verification. The ISI profile excludes ambiguous mappings; unsupported input can fail validation.

Unicode Sinhala fonts share an encoding, so changing between Unicode fonts normally means selecting a different font in the destination app. It does not require a legacy conversion. The **Target font** list shows installed Windows fonts; appearing in that list does not establish Sinhala support or a compatible legacy encoding.

## Converting text

1. Paste or type text in **Input text**.
2. Choose **Source encoding**:
   - **Auto detect** for Sinhala Unicode or copied text with useful font metadata.
   - **Unicode Sinhala** for text already in Unicode.
   - A named legacy profile when you know the original encoding.
   - **Wijesekara keys** for raw keyboard sequences typed in the panel.
   - **Singlish sounds** for phonetic input such as `sinhala` or `dhu`.
3. Choose **Target encoding** and a matching **Target font**.
4. Review **Result** and, for legacy targets, **Sinhala text (Unicode preview)**.
5. Click **Copy result**, then paste into the destination app.

The preview updates as you edit. **Convert** also runs conversion explicitly.

### Example: the papilla in දු

For FM Abhaya output:

| Setting | Value |
| --- | --- |
| Source encoding | Wijesekara keys |
| Input text | `oq` — the keys `o` then `q` |
| Target encoding | FM Abhaya (legacy) |
| Target font | Your installed FM Abhaya font, for example `FMAbhaya` |
| Unicode preview | දු |
| Stored legacy output | `ÿ` |

The legacy character `ÿ` is the font's code for the special glyph. It displays as Sinhala when rendered with the matching font. Likewise, `us` produces **මි**, stored as `ñ` in this mapping. Literal `ou` means **දම** in the bundled FM key map.

With **Singlish sounds** selected as the source, `dhu` also produces **දු**.

### Auto detection and clipboard conversion

Plain Latin-coded text cannot reliably identify its original legacy font. For example, `oq` alone does not prove that the text uses FM Abhaya. If detection is unknown, Result stays empty and copying is disabled until you choose a source.

To convert copied text without opening the panel first:

1. Set the app's source, target, and font choices.
2. Copy text in another application.
3. Press **Ctrl+Alt+Shift+S**.
4. Paste the converted result, or review the panel if the app requests confirmation of the source.

The shortcut copies automatically only when the source is sufficiently certain, conversion has no warnings, and the matching target font is installed. A font name found in rich clipboard formatting is a hint that requires review.

## Live typing

1. Select a supported **legacy target encoding** and matching **Target font**.
2. Set **Typing style** to **Wijesekara keys** or **Singlish sounds**.
3. In the destination application, select the same font and focus an editable text field.
4. Press **Ctrl+Alt+Shift+T** to start typing assistance for that window.
5. Type normally. Press the shortcut again or **Esc** to stop.

| Wijesekara input | Singlish input | Sinhala |
| --- | --- | --- |
| `ls` | `ki` | කි |
| `lq` | `ku` | කු |
| `us` | `mi` | මි |
| `uq` | `mu` | මු |
| `oq` | `dhu` | දු |

The helper recomposes the current word and sends the selected legacy character codes. Backspace edits the in-progress word. Spaces, tabs, Enter, navigation keys, and modifier shortcuts end the current composition. A change of foreground window stops typing assistance when the next key is processed.

Use an **English/US physical keyboard layout** for this mode. Live Singlish processes letter keys; punctuation passes through and ends the current word. Some special Singlish sequences supported by the panel are therefore unavailable during live typing. For Unicode typing directly in other apps, use a Windows Sinhala Unicode keyboard.

## Keyboard shortcuts and tray behavior

| Action | Shortcut / control |
| --- | --- |
| Convert copied text | **Ctrl+Alt+Shift+S** |
| Toggle live typing for the focused external window | **Ctrl+Alt+Shift+T** |
| Stop live typing | **Esc**, the typing shortcut, or tray menu **Stop live typing** |
| Reopen the app | Run the EXE again, double-click its tray icon, or choose **Open** |
| Hide the window | Close the window with **X** |
| Quit completely | Tray menu **Exit** |

Shortcuts are fixed in the current version. Only one updated copy runs per Windows user/session, even when launched from different folders. Repeat launches signal the existing window before creating any new tray icon or typing hook.

## Using the result in Photoshop and other apps

1. Convert to the encoding expected by your chosen font.
2. Create or select an editable text layer or text field in the destination app.
3. Select the same font there.
4. Paste the result, or activate live typing while that field is focused.

Apps may ignore the rich-text font hint and use their currently selected font. Legacy output viewed in a normal Latin font can look like accented Latin characters. The Unicode preview helps check the intended Sinhala reading.

The app does not inspect or change Photoshop layers, install fonts, or configure the destination application's text engine. Live typing has not been independently verified inside Photoshop; test a short phrase in your own application before using it for longer text.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| Result shows Latin or accented characters | Select the legacy font that matches the target encoding in both the preview and destination app. Check the Unicode reading. |
| `oq` produces no result with Auto detect | Select **Wijesekara keys** as the source when entering raw keys, or the known legacy source profile for pasted text. |
| Copy result is disabled | Check for an unknown source, empty input, mismatched font name, unmapped characters, or a round-trip warning. |
| A font is absent from the list | Install it separately and restart the app to refresh the installed-font list. |
| Typing shortcut opens the panel | Choose a supported legacy target and matching font, then focus a text field in another application before pressing the shortcut. |
| Global shortcut is unavailable | Another app or an older copy may own it. Exit the old copy; panel conversion remains available. |
| Typing does not reach another app | Check that the field accepts text and that it is not running at a higher privilege level than Sinhala Font Bridge. Use panel conversion and paste if necessary. |
| Live typing stops mid-word | Check the status message. Unsupported sequences, a window change, or the 48-key word limit can stop the session. |
| Text looks correct in the preview but wrong after pasting | Set the matching font in the receiving app and check its Sinhala rendering support. |
| Closing the window does not stop the app | This is tray behavior. Choose **Exit** from the notification-area menu. |
| Imported profile is not visible | Exit the app completely and restart it. A second launch alone restores the existing process. |
| App fails after a custom profile import | Exit the app, move the problematic JSON out of the local Profiles folder, then restart. |

## Privacy and local data

Conversion and composition happen locally. The application code does not upload text, use an online conversion API, or include analytics.

- A Windows keyboard hook is installed only while live typing is active. It keeps the current word in memory and does not write a keystroke log.
- Text pasted into the conversion panel remains in memory while the app is open. Copying output places it on the Windows clipboard.
- Preferences are stored in `%LOCALAPPDATA%\SinhalaFontBridge\settings.json`.
- Imported profiles are stored in `%LOCALAPPDATA%\SinhalaFontBridge\Profiles\`.

Windows clipboard history and receiving applications manage copied text according to their own settings. To remove this portable app, exit it and delete its extracted folder. Delete its local data folder separately if you also want to remove preferences and imported profiles.

## Building and testing

Use the **.NET 10 SDK on Windows**. Python and Node.js are optional and only needed to regenerate upstream mapping data.

### Build

```powershell
dotnet build SinhalaFontBridge/SinhalaFontBridge.csproj -c Release
```

The development executable is written under `SinhalaFontBridge/bin/Release/net10.0-windows/`.

### Run the checks

```powershell
dotnet run --project SinhalaFontBridge.Checks/SinhalaFontBridge.Checks.csproj -c Release
dotnet run --project SinhalaFontBridge.UiChecks/SinhalaFontBridge.UiChecks.csproj -c Release
```

The core checks cover published FM examples, source detection, conversion failure handling, mapping parity fixtures, common phrase round trips, and incremental typing prefixes. The Windows UI checks cover actual panel controls, the special FM papilla output, copy availability, window restoration, duplicate processes, and restart after normal exit or a crash.

The UI checks need an interactive Windows desktop. They create a hidden app window and use isolated names for the process-lock tests. These checks do not establish compatibility with every font or external application.

### Publish a portable Windows package

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish.ps1
```

The script publishes a self-contained x64 build and packages the EXE, mapping profiles, Singlish table, README, and license notices:

```text
release/
  win-x64/
    SinhalaFontBridge.exe
    singlish_keys.json
    Profiles/
    licenses/
    README.md
  SinhalaFontBridge-win-x64.zip
```

For a manual publish without ZIP packaging:

```powershell
dotnet publish SinhalaFontBridge/SinhalaFontBridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o release/win-x64
```

Even with a single-file EXE, the external profiles and Singlish table must accompany it. Publishing does not create an installer or upload a GitHub release.

## Custom mapping profiles

Click **Import mapping profile**, choose a JSON file, then **Exit** and restart the app. Use a unique `id`; a duplicate ID does not override a bundled profile.

A minimal dictionary-based profile looks like this:

```json
{
  "id": "my_legacy_font",
  "fontName": "My Legacy Font",
  "aliases": ["MyLegacyFont"],
  "rules": {},
  "singles": { "l": "ක" },
  "combos": { "ld": "කා" }
}
```

- `fontName` and optional `aliases` identify compatible target font names.
- `singles` and `combos` map legacy strings to Unicode Sinhala.
- Optional `rules` run as literal preprocessing replacements before decoding.
- Dictionary profiles derive reverse mappings where the individual mapping decodes correctly.
- Profiles may instead supply ordered `decodeSteps` and optional `encodeSteps`, each with a regex `pattern` and literal `replacement`.
- A profile with only `decodeSteps` is available as a source, not as a legacy output target.
- Legacy encoding is checked by a full round trip before copying is enabled.

The example is illustrative and intentionally covers only two forms. An installed font file alone is insufficient to derive a correct conversion profile.

## Project structure

```text
SinhalaFontBridge/           Windows Forms UI, clipboard, shortcuts, typing hook, instance guard
SinhalaFontBridge.Core/      Conversion engine, typing composer, profiles, Singlish table
SinhalaFontBridge.Checks/    Core regression checks and reference fixtures
SinhalaFontBridge.UiChecks/  Windows UI and process-lifecycle checks
third_party/                Mapping provenance and license notices
tools/                      Mapping import/generation helpers and packaging script
```

Mapping-generation helpers accept separately obtained upstream source files:

```powershell
python tools/import_open_sl.py PATH_TO_OPEN_SL SinhalaFontBridge.Core/Profiles
node tools/generate_singlish_map.js PATH_TO_OPEN_SL SinhalaFontBridge.Core/singlish_keys.json
node tools/generate_open_sl_fixtures.js PATH_TO_OPEN_SL SinhalaFontBridge.Checks/open_sl_fixtures.json
python tools/import_isi.py PATH_TO_DATA_LIST_JSON SinhalaFontBridge.Core/Profiles/isi_island.json
```

Normal builds use committed mappings and fixtures; no upstream checkout is needed. Review generated changes and rerun the checks before submitting them. Research downloads, local captures, build output, and packaged binaries are excluded from the repository.

## Limitations

- Plain legacy text can be ambiguous: automatic identification of every font is not possible from the characters alone.
- A matching profile is required for each legacy encoding. Font-family aliases do not guarantee every variant has identical glyph behavior.
- Round-trip checks establish consistency with the mapping, not visual correctness in every installed font.
- Mixed English and Sinhala in one legacy-font run may need separate formatting.
- Amalee and Thibus are decode-only profiles.
- Live typing expects a normal editable field, an English/US key layout, and short in-progress words. Moving the caret with the mouse inside a word is not tracked; stop and restart typing assistance after repositioning it.
- The app does not perform OCR, convert images/PDF layouts, or preserve a document's rich formatting through conversion.
- This is a Windows desktop app; macOS, Linux, mobile, and universal application compatibility are outside the current implementation.

## Contributing

Issues and pull requests are welcome. For a conversion problem, include:

- The exact input text or physical keys pressed.
- Source encoding, target encoding, and exact font name/version.
- Expected Sinhala text and the actual output; a screenshot helps with glyph problems.
- Windows version, destination application, and whether the issue occurs in the panel or live typing.

Use short, non-sensitive examples. For new font support, provide a mapping source and its license, plus representative decode and encode samples. Add regression coverage for changed behavior and run the relevant checks before submitting a pull request.

## Credits and licensing

The bundled mapping data retains its upstream attribution and terms:

| Source | Used for | Local notice |
| --- | --- | --- |
| [akuruAI/Pandukabhaya](https://github.com/akuruAI/Pandukabhaya) | FM Abhaya mapping and published test examples | [MIT license](third_party/pandukabhaya/LICENSE), [provenance](third_party/pandukabhaya/NOTICE.md) |
| [Open-SL/sinhala-unicode-converter](https://github.com/Open-SL/sinhala-unicode-converter) | DL Manel, Kaputa, Amalee, Thibus, Singlish tables, parity fixtures | [Attribution and upstream MIT declaration](third_party/open-sl/NOTICE.md) |
| [kawishkamd/sinhala-legacy-converter](https://github.com/kawishkamd/sinhala-legacy-converter) | Filtered ISI/Island mapping | [MIT license](third_party/isi/LICENSE), [provenance](third_party/isi/NOTICE.md) |

Open-SL credits the Language Technology Research Laboratory at the University of Colombo School of Computing for the original translation code. Font-family grouping was informed by a [UCSC font study](https://dl.ucsc.cmb.ac.lk/jspui/bitstream/123456789/4822/1/2018MCS011.pdf).

No proprietary font files are bundled. Font licenses are separate from conversion-data licenses. Bamini routines found upstream were excluded because they target Tamil.

A project-wide license for the application code has not yet been selected; the third-party notices above apply to their respective materials.
