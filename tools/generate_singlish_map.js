// Extract ordered transliteration tables from Open-SL's MIT-declared converter.
// Usage: node tools/generate_singlish_map.js research/open-sl output.json
const fs = require('fs');
const vm = require('vm');
const path = require('path');

const [repo, destination] = process.argv.slice(2);
if (!repo || !destination) throw new Error('Expected repository and output paths');
const file = path.join(repo, 'src', 'translators', 'singlish_to_unicode.ts');
const source = fs.readFileSync(file, 'utf8')
  .replace(/let nVowels:number;/, 'let nVowels;')
  .replace('function(text: string)', 'function(text)')
  .replace('export const singlishToUnicode', 'var singlishToUnicode');
const context = {};
vm.runInNewContext(source + '\nthis.tables = { vowels, vowelsUni, vowelModifiersUni, consonants, consonantsUni, specialChar, specialCharUni };', context, {filename:file});
const t = context.tables;
const pairs = (a,b) => Array.from(a, (key,i) => [key,b[i]]);
const result = {
  source: 'https://github.com/Open-SL/sinhala-unicode-converter',
  vowels: Array.from(t.vowels, (key,i) => [key.replace(/\\([()])/g, '$1'), t.vowelsUni[i], t.vowelModifiersUni[i]]),
  consonants: pairs(t.consonants,t.consonantsUni),
  specialChars: pairs(t.specialChar,t.specialCharUni),
  examples: Object.fromEntries(['ki','ku','mi','mu','sinhala','shrii','laMkaa','amma','ka'].map(x => [x,context.singlishToUnicode(x)]))
};
fs.writeFileSync(destination, JSON.stringify(result,null,2)+'\n','utf8');
