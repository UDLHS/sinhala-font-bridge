using System.Text.Json;

namespace SinhalaFontBridge.Core;

public enum TypingStyle { Wijesekara, Singlish }

// Composes a short in-progress word. The global typing service then encodes
// this Unicode text with the selected legacy profile before sending it.
public sealed class TypingComposer
{
    private readonly LegacyProfile _fmKeyboard;
    private readonly string[][] _vowels;
    private readonly string[][] _consonants;
    private readonly string[][] _specialChars;

    public TypingComposer(ConversionEngine engine, string tablePath)
    {
        _fmKeyboard = engine.Profiles.Single(p => p.Id == "fm_abhaya");
        using var document = JsonDocument.Parse(File.ReadAllText(tablePath));
        _vowels = ReadPairs(document.RootElement, "vowels");
        _consonants = ReadPairs(document.RootElement, "consonants");
        _specialChars = ReadPairs(document.RootElement, "specialChars");
    }

    public string Compose(string keys, TypingStyle style) => style == TypingStyle.Wijesekara
        ? _fmKeyboard.Decode(keys)
        : ComposeSinglish(keys);

    private string ComposeSinglish(string text)
    {
        // A common Sinhala phonetic sequence: sin-ha-la -> සිංහල.
        text = text.Replace("nh", "ංh", StringComparison.Ordinal);
        text = text.Replace("\\n", "ං", StringComparison.Ordinal)
            .Replace("\\h", "ඃ", StringComparison.Ordinal)
            .Replace("\\N", "ඞ", StringComparison.Ordinal)
            .Replace("\\R", "ඍ", StringComparison.Ordinal);
        foreach (var special in _specialChars)
            foreach (var consonant in _consonants)
                text = text.Replace(consonant[0] + special[0], consonant[1] + special[1], StringComparison.Ordinal);
        foreach (var consonant in _consonants)
        {
            foreach (var vowel in _vowels)
                text = text.Replace(consonant[0] + "r" + vowel[0], consonant[1] + "්‍ර" + vowel[2], StringComparison.Ordinal);
            text = text.Replace(consonant[0] + "r", consonant[1] + "්‍ර", StringComparison.Ordinal);
        }
        foreach (var consonant in _consonants)
            foreach (var vowel in _vowels)
                text = text.Replace(consonant[0] + vowel[0], consonant[1] + vowel[2], StringComparison.Ordinal);
        foreach (var consonant in _consonants)
            text = text.Replace(consonant[0], consonant[1] + "්", StringComparison.Ordinal);
        foreach (var vowel in _vowels)
            text = text.Replace(vowel[0], vowel[1], StringComparison.Ordinal);
        return text;
    }

    private static string[][] ReadPairs(JsonElement root, string name) => root.GetProperty(name)
        .EnumerateArray().Select(row => row.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()).ToArray();
}
