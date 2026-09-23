// Run against a checked-out Open-SL repository to compare the .NET port with
// the original JavaScript functions. This file does not modify source routines.
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const [repo, destination] = process.argv.slice(2);
if (!repo || !destination) throw new Error('usage: node generate_open_sl_fixtures.js REPO OUTPUT');

const names = {
  dl_manel: ['dl_manel_to_unicode', 'unicode_to_dl_manel'],
  kaputa: ['kaputa_to_unicode', 'unicode_to_kaputa'],
  amalee: ['amalee_to_unicode', null],
  thibus: ['thibus_to_unicode', null]
};

function load(name) {
  const file = path.join(repo, 'src', 'translators', `${name}.ts`);
  const source = fs.readFileSync(file, 'utf8')
    .replace(/^export const \w+ = function\s*\(text: string\)/, 'var convert = function (text)');
  const context = {};
  vm.runInNewContext(source, context, { filename: file });
  return context.convert;
}

const legacySamples = [
  'wdKavql%u jHjia:dj', 'Y%S ,xld', 'rvQ[E m@n`j~',
  'o<od ud,s.dj', 'fYa% ff,l', 'wïud',
  'ff;% fVHda ;= m% %s fka', 'a`N~dEkYm v&vs~}`v'
];
const unicodeSamples = [
  'ශ්‍රී ලංකා', 'ආණ්ඩුක්‍රම ව්‍යවස්ථාව', 'සිංහල අකුරු',
  'රවිඳු මනොජ්', 'අම්මා', 'දළදා මාලිගාව', 'කෙ කැ කෝ ක්‍රි'
];

const fixtures = {};
for (const [id, [decodeName, encodeName]] of Object.entries(names)) {
  const decode = load(decodeName);
  const encode = encodeName && load(encodeName);
  fixtures[id] = {
    decode: legacySamples.map(input => ({ input, expected: decode(input) })),
    encode: encode ? unicodeSamples.map(input => ({ input, expected: encode(input) })) : []
  };
}
fs.writeFileSync(destination, JSON.stringify(fixtures, null, 2) + '\n', 'utf8');
