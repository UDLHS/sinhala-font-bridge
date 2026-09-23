# Open-SL converter attribution

The `dl_manel.json`, `kaputa.json`, `amalee.json`, and `thibus.json` profiles are generated from the ordered replacement rules in [Open-SL/sinhala-unicode-converter](https://github.com/Open-SL/sinhala-unicode-converter). The `singlish_keys.json` live typing table is generated from its `singlish_to_unicode.ts` input mapping. Its `package.json` declares the MIT license and lists Janaka Chathuranga as author. The upstream README credits the Language Technology Research Laboratory at the University of Colombo School of Computing for the original translation code.

The generated profiles preserve replacement order. `tools/import_open_sl.py` and `tools/generate_singlish_map.js` record the conversion procedures. The Bamini routines in the same source are for Tamil and are excluded from this Sinhala app.

See the [upstream package metadata](https://github.com/Open-SL/sinhala-unicode-converter/blob/master/package.json) for its license declaration and source repository.
