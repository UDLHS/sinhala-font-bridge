using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SinhalaFontBridge.Core;

public sealed record Detection(string SourceId, string Explanation, bool IsCertain);
public sealed record ConversionResult(string Text, string UnicodeText, string SourceId, string TargetId, IReadOnlyList<string> Warnings, bool CanCopy);

public sealed class LegacyProfile
{
    public string Id { get; }
    public string FontName { get; }
    public IReadOnlyList<string> Aliases { get; }
    public bool CanEncode => _encodeSteps.Length > 0 || _reverse.Count > 0;
    public bool MatchesFont(string fontName) => new[] { FontName }.Concat(Aliases)
        .Any(name => NormalizeFontName(name) == NormalizeFontName(fontName));
    private readonly Dictionary<string, string> _rules;
    private readonly Dictionary<string, string> _forward;
    private readonly Dictionary<string, string> _reverse;
    private readonly string[] _forwardKeys;
    private readonly string[] _reverseKeys;
    private readonly string[] _ruleKeys;
    private readonly RegexStep[] _decodeSteps;
    private readonly RegexStep[] _encodeSteps;

    private LegacyProfile(string id, string fontName, IReadOnlyList<string> aliases,
        Dictionary<string, string> rules, Dictionary<string, string> forward,
        RegexStep[] decodeSteps, RegexStep[] encodeSteps)
    {
        Id = id;
        FontName = fontName;
        Aliases = aliases;
        _rules = rules;
        _forward = forward;
        _decodeSteps = decodeSteps;
        _encodeSteps = encodeSteps;
        _ruleKeys = rules.Keys.OrderByDescending(x => x.Length).ToArray();
        _forwardKeys = forward.Keys.OrderByDescending(x => x.Length).ToArray();

        // Several legacy strings represent the same Unicode text. Pick a short,
        // reproducible spelling, then verify the complete result after encoding.
        _reverse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in forward.GroupBy(x => x.Value, StringComparer.Ordinal))
        {
            var candidate = group.Select(x => x.Key)
                .Where(key => Decode(key) == group.Key)
                .OrderBy(key => key.Length)
                .ThenBy(key => key.Count(c => c > 127))
                .ThenBy(key => key, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is not null) _reverse[group.Key] = candidate;
        }
        _reverseKeys = _reverse.Keys.OrderByDescending(x => x.Length).ToArray();
    }

    public static LegacyProfile Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var root = document.RootElement;
        string id = root.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? Path.GetFileNameWithoutExtension(path)
            : root.GetProperty("metadata").GetProperty("mapping").GetProperty("name").GetString() ?? Path.GetFileNameWithoutExtension(path);
        string fontName = root.TryGetProperty("fontName", out var nameElement)
            ? nameElement.GetString() ?? id
            : root.GetProperty("metadata").GetProperty("font").GetProperty("name").GetString() ?? id;
        var mappings = root.TryGetProperty("mappings", out var nested) ? nested : root;
        var rules = ReadDictionary(mappings, "rules");
        var forward = ReadDictionary(mappings, "singles");
        foreach (var pair in ReadDictionary(mappings, "combos")) forward[pair.Key] = pair.Value;
        var decodeSteps = ReadSteps(root, "decodeSteps");
        var encodeSteps = ReadSteps(root, "encodeSteps");
        var aliases = root.TryGetProperty("aliases", out var aliasElement)
            ? aliasElement.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
            : Array.Empty<string>();
        if (forward.Count == 0 && decodeSteps.Length == 0) throw new InvalidDataException($"Profile {path} has no decoder.");
        return new LegacyProfile(id, fontName, aliases, rules, forward, decodeSteps, encodeSteps);
    }

    private static Dictionary<string, string> ReadDictionary(JsonElement root, string property)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty(property, out var element))
            foreach (var pair in element.EnumerateObject())
                if (!string.IsNullOrEmpty(pair.Name) && pair.Value.ValueKind == JsonValueKind.String)
                    result[pair.Name] = pair.Value.GetString() ?? string.Empty;
        return result;
    }

    private static RegexStep[] ReadSteps(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var steps)) return [];
        return steps.EnumerateArray()
            .Select(step => new RegexStep(step.GetProperty("pattern").GetString() ?? "",
                step.GetProperty("replacement").GetString() ?? ""))
            .ToArray();
    }

    public string Decode(string text) => _decodeSteps.Length > 0
        ? ApplySteps(text, _decodeSteps)
        : ReplaceLongest(ReplaceLongest(text, _rules, _ruleKeys), _forward, _forwardKeys);

    public (string Text, int Unmapped) Encode(string text)
    {
        if (_encodeSteps.Length > 0)
        {
            var converted = ApplySteps(text, _encodeSteps);
            return (converted, converted.Count(IsSinhala));
        }
        var output = new StringBuilder();
        int unmapped = 0;
        for (int i = 0; i < text.Length;)
        {
            var match = _reverseKeys.FirstOrDefault(key => text.AsSpan(i).StartsWith(key, StringComparison.Ordinal));
            if (match is not null)
            {
                output.Append(_reverse[match]);
                i += match.Length;
            }
            else
            {
                char c = text[i++];
                output.Append(c);
                if (IsSinhala(c)) unmapped++;
            }
        }
        return (output.ToString(), unmapped);
    }

    public static bool IsSinhala(char c) => c is >= '\u0D80' and <= '\u0DFF';

    private static string NormalizeFontName(string name) =>
        new(name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string ApplySteps(string text, RegexStep[] steps)
    {
        foreach (var step in steps) text = step.Pattern.Replace(text, _ => step.Replacement);
        return text;
    }

    private sealed class RegexStep
    {
        public Regex Pattern { get; }
        public string Replacement { get; }

        public RegexStep(string pattern, string replacement)
        {
            Pattern = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            Replacement = replacement;
        }
    }

    private static string ReplaceLongest(string input, Dictionary<string, string> mapping, string[] keys)
    {
        if (keys.Length == 0) return input;
        var output = new StringBuilder();
        for (int i = 0; i < input.Length;)
        {
            var match = keys.FirstOrDefault(key => input.AsSpan(i).StartsWith(key, StringComparison.Ordinal));
            if (match is not null)
            {
                output.Append(mapping[match]);
                i += match.Length;
            }
            else output.Append(input[i++]);
        }
        return output.ToString();
    }
}

public sealed class ConversionEngine
{
    public const string UnicodeId = "unicode";
    public IReadOnlyList<LegacyProfile> Profiles { get; }

    public ConversionEngine(IEnumerable<LegacyProfile> profiles)
    {
        Profiles = profiles.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToArray();
    }

    public static ConversionEngine LoadDefault(string baseDirectory, string? extraDirectory = null)
    {
        var files = Directory.Exists(Path.Combine(baseDirectory, "Profiles"))
            ? Directory.GetFiles(Path.Combine(baseDirectory, "Profiles"), "*.json")
            : Array.Empty<string>();
        if (extraDirectory is not null && Directory.Exists(extraDirectory))
            files = files.Concat(Directory.GetFiles(extraDirectory, "*.json")).ToArray();
        return new ConversionEngine(files.Select(LegacyProfile.Load));
    }

    public Detection Detect(string text, string? fontHint = null)
    {
        if (text.Any(LegacyProfile.IsSinhala))
            return new Detection(UnicodeId, "Sinhala Unicode code points are present.", true);
        var matches = Profiles.Where(p => fontHint is not null &&
            new[] { p.FontName }.Concat(p.Aliases)
                .Any(name => fontHint.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                    fontHint.Contains(name.Replace('-', ' '), StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 1)
            return new Detection(matches[0].Id, $"Copied formatting mentions {matches[0].FontName}; check the preview.", false);
        return new Detection("unknown", text.Any(char.IsLetter)
            ? "Plain Latin-coded text does not identify its Sinhala legacy font. Choose the source profile."
            : "No Sinhala text or identifiable legacy font was found.", false);
    }

    public ConversionResult Convert(string text, string sourceId, string targetId, string? fontHint = null)
    {
        var warnings = new List<string>();
        var source = sourceId == "auto" ? Detect(text, fontHint).SourceId : sourceId;
        if (source == "unknown")
            return new ConversionResult("", "", source, targetId, new[] { Detect(text, fontHint).Explanation }, false);
        var sourceProfile = source == UnicodeId ? null : Profiles.FirstOrDefault(p => p.Id == source);
        var targetProfile = targetId == UnicodeId ? null : Profiles.FirstOrDefault(p => p.Id == targetId);
        if (source != UnicodeId && sourceProfile is null || targetId != UnicodeId && targetProfile is null)
            return new ConversionResult(text, "", source, targetId, new[] { "Unknown conversion profile." }, false);
        if (targetProfile is { CanEncode: false })
            return new ConversionResult(text, "", source, targetId, new[] { "This font has a decoder but no verified encoder." }, false);
        string unicode = sourceProfile?.Decode(text) ?? text;
        string output = unicode;
        bool canCopy = true;
        if (targetProfile is not null)
        {
            var encoded = targetProfile.Encode(unicode);
            output = encoded.Text;
            if (encoded.Unmapped > 0)
            {
                warnings.Add($"{encoded.Unmapped} Sinhala character(s) are not covered by {targetProfile.FontName}. Output is incomplete.");
                canCopy = false;
            }
            if (unicode.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
                warnings.Add("Latin letters will appear as Sinhala glyphs if the entire result uses the legacy font. Format those spans separately.");
            if (targetProfile.Decode(output) != unicode)
            {
                warnings.Add("Round-trip verification failed for this text. Review the output before using it.");
                canCopy = false;
            }
        }
        return new ConversionResult(output, unicode, source, targetId, warnings, canCopy);
    }
}
