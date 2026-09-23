using System.Text.Json;
using SinhalaFontBridge.Core;

var engine = ConversionEngine.LoadDefault(AppContext.BaseDirectory);
var composer = new TypingComposer(engine, Path.Combine(AppContext.BaseDirectory, "singlish_keys.json"));
int typingPrefixChecks = 0;
foreach (var sample in new[] { ("ki", "කි"), ("ku", "කු"), ("mi", "මි"), ("mu", "මු"), ("sinhala", "සිංහල") })
    Check(composer.Compose(sample.Item1, TypingStyle.Singlish) == sample.Item2, "Singlish pilla: " + sample.Item1);
foreach (var sample in new[] { ("ls", "කි"), ("lq", "කු"), ("us", "මි"), ("uq", "මු"), ("oq", "දු") })
    Check(composer.Compose(sample.Item1, TypingStyle.Wijesekara) == sample.Item2, "Wijesekara pilla: " + sample.Item1);
Check(engine.Profiles.Single(p => p.Id == "fm_abhaya").Encode("මි").Text == "ñ", "FM special ispilla glyph for මි");
Check(engine.Profiles.Single(p => p.Id == "fm_abhaya").Encode("දු").Text == "ÿ", "FM special papilla glyph for දු");
var papillaPanelResult = engine.Convert("දු", ConversionEngine.UnicodeId, "fm_abhaya");
var ambiguousPanelResult = engine.Convert("oq", "auto", "fm_abhaya");
Check(!ambiguousPanelResult.CanCopy && ambiguousPanelResult.Text == "", "Unknown source must not display unconverted oq as a result");
Check(papillaPanelResult.Text == "ÿ" && papillaPanelResult.UnicodeText == "දු", "Panel keeps legacy code and readable Sinhala separate");
Check(engine.Convert("oq", "fm_abhaya", ConversionEngine.UnicodeId).Text == "දු", "FM keys o and q make දු");
Check(engine.Convert("ou", "fm_abhaya", ConversionEngine.UnicodeId).Text == "දම", "FM key u is ම, not a papilla");
Check(engine.Convert(composer.Compose("oq", TypingStyle.Wijesekara), ConversionEngine.UnicodeId, "fm_abhaya").Text == "ÿ",
    "Wijesekara panel source composes special FM papilla glyph");
Check(engine.Convert(composer.Compose("dhu", TypingStyle.Singlish), ConversionEngine.UnicodeId, "fm_abhaya").Text == "ÿ",
    "Singlish panel source composes special FM papilla glyph");
foreach (var profile in engine.Profiles.Where(p => p.CanEncode))
    foreach (var sample in new[] { ("ki", TypingStyle.Singlish), ("ku", TypingStyle.Singlish),
        ("mi", TypingStyle.Singlish), ("mu", TypingStyle.Singlish), ("sinhala", TypingStyle.Singlish),
        ("ls", TypingStyle.Wijesekara), ("lq", TypingStyle.Wijesekara),
        ("us", TypingStyle.Wijesekara), ("uq", TypingStyle.Wijesekara), ("oq", TypingStyle.Wijesekara) })
        for (int length = 1; length <= sample.Item1.Length; length++)
        {
            string unicode = composer.Compose(sample.Item1[..length], sample.Item2);
            var encoded = profile.Encode(unicode);
            Check(encoded.Unmapped == 0 && profile.Decode(encoded.Text) == unicode,
                $"Live typing prefix {profile.Id} {sample.Item1[..length]} -> {unicode}");
            typingPrefixChecks++;
        }
if (engine.Profiles.Count != 6) throw new Exception($"Expected six Sinhala legacy encoding profiles, got {engine.Profiles.Count}.");
var examples = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "test_cases.json")));
int count = 0;
foreach (var pair in examples.RootElement[0].GetProperty("test_cases").EnumerateArray())
{
    string legacy = pair[0].GetString()!;
    string expected = pair[1].GetString()!;
    var actual = engine.Convert(legacy, "fm_abhaya", ConversionEngine.UnicodeId);
    if (actual.Text != expected) throw new Exception($"FM Abhaya example {count + 1} failed.\nExpected: {expected}\nActual: {actual.Text}");
    count++;
}

Check(engine.Detect("සිංහල").SourceId == ConversionEngine.UnicodeId, "Unicode detection");
Check(engine.Detect("hello world").SourceId == "unknown", "Do not mistake English for FM Abhaya");
Check(engine.Detect("rkaosl", "FM Abhaya").SourceId == "fm_abhaya", "Font metadata hint");
Check(engine.Detect("rkaosl", "FM-Malithi").SourceId == "fm_abhaya", "FM family font hint");
Check(engine.Profiles.Single(p => p.Id == "dl_manel").MatchesFont("DL Champika"), "DL family font names");
Check(!engine.Profiles.Any(p => p.Id == "bamini"), "Tamil Bamini must not be bundled");
Check(!engine.Convert("rkaosl", "auto", ConversionEngine.UnicodeId).CanCopy, "Ambiguous text must not auto-copy");
foreach (string sample in new[] { "ශ්‍රී ලංකා", "සිංහල අකුරු", "දළදා මාලිගාව" })
{
    var encoded = engine.Convert(sample, ConversionEngine.UnicodeId, "fm_abhaya");
    Check(encoded.CanCopy, "Unicode to FM Abhaya: " + sample);
    Check(engine.Convert(encoded.Text, "fm_abhaya", ConversionEngine.UnicodeId).Text == sample, "Round trip: " + sample);
}
Check(!engine.Convert("ෳ", ConversionEngine.UnicodeId, "fm_abhaya").CanCopy, "Unmapped Sinhala must be blocked");
var portFixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "open_sl_fixtures.json")));
int portChecks = 0;
foreach (var profileFixtures in portFixtures.RootElement.EnumerateObject())
{
    var profile = engine.Profiles.Single(p => p.Id == profileFixtures.Name);
    foreach (var item in profileFixtures.Value.GetProperty("decode").EnumerateArray())
    {
        string input = item.GetProperty("input").GetString()!;
        string expected = item.GetProperty("expected").GetString()!;
        Check(profile.Decode(input) == expected, $"{profile.Id} decode port: {input}");
        portChecks++;
    }
    foreach (var item in profileFixtures.Value.GetProperty("encode").EnumerateArray())
    {
        string input = item.GetProperty("input").GetString()!;
        string expected = item.GetProperty("expected").GetString()!;
        Check(profile.Encode(input).Text == expected, $"{profile.Id} encode port: {input}");
        portChecks++;
    }
}
Check(engine.Convert("wdKavql%u jHjia:dj", "dl_manel", ConversionEngine.UnicodeId).Text == "ආණ්ඩුක්‍රම ව්‍යවස්ථාව", "DL Manel published example");
Check(engine.Convert("rvQ[E m@n`j~", "kaputa", ConversionEngine.UnicodeId).Text == "රවිඳු මනොජ්", "Kaputa published example");
Check(!engine.Profiles.Single(p => p.Id == "amalee").CanEncode, "Amalee is decode only");
Check(!engine.Profiles.Single(p => p.Id == "thibus").CanEncode, "Thibus is decode only");
foreach (var profile in engine.Profiles.Where(p => p.Id is "dl_manel" or "kaputa" or "isi_island"))
{
    int passed = 0;
    foreach (var sample in new[] { "ශ්‍රී ලංකා", "ආණ්ඩුක්‍රම ව්‍යවස්ථාව", "සිංහල අකුරු", "රවිඳු මනොජ්", "අම්මා", "දළදා මාලිගාව", "කෙ කැ කෝ ක්‍රි" })
    {
        var converted = engine.Convert(sample, ConversionEngine.UnicodeId, profile.Id);
        if (converted.CanCopy) passed++;
        else Console.WriteLine($"{profile.Id} cannot round trip {sample}: {converted.Text}; {string.Join(" | ", converted.Warnings)}");
    }
    Console.WriteLine($"{profile.Id} verified Unicode samples: {passed}/7");
    Check(passed == 7, profile.Id + " should round trip common Sinhala phrases");
}
Console.WriteLine($"PASS: {count} FM examples, 14 FM behavior checks, {portChecks} Open-SL parity checks, {typingPrefixChecks} live typing prefixes, and 4 profile checks.");

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
}
