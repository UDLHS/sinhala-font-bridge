# ISI/Island mapping attribution

The `isi_island.json` profile is generated from `data_list.json` in [kawishkamd/sinhala-legacy-converter](https://github.com/kawishkamd/sinhala-legacy-converter), MIT licensed by Kavishka Dahanayaka in 2025. That project credits [gavi-tharaka/sinhala_convertor](https://github.com/gavi-tharaka/sinhala_convertor) as its foundation.

`tools/import_isi.py` excludes the source table's whitespace-to-Sinhala row and conflicting legacy keys. When a key conflicts only between a Sinhala letter and quotation punctuation, it chooses the Sinhala letter; punctuation cases that cannot round trip are blocked by the app.
