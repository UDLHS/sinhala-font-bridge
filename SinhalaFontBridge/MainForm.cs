using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using SinhalaFontBridge.Core;

namespace SinhalaFontBridge;

public sealed class MainForm : Form
{
    private const int HotkeyId = 0x5141;
    private const int TypingHotkeyId = 0x5142;
    private const string WijesekaraId = "wijesekara";
    private const string SinglishId = "singlish";
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004;
    private readonly ConversionEngine _engine;
    private readonly ComboBox _source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _target = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _font = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
    private readonly ComboBox _typingStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 165 };
    private readonly Label _typingState = new() { Text = "Live typing: off", AutoSize = true, Padding = new Padding(10, 6, 0, 0) };
    private readonly TextBox _input = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Nirmala UI", 12) };
    private readonly TextBox _output = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Nirmala UI", 12) };
    private readonly TextBox _unicodeCheck = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Nirmala UI", 12) };
    private readonly Label _resultCaption = new() { Text = "Result", Dock = DockStyle.Fill };
    private readonly Label _unicodeCaption = new() { Text = "Sinhala text (Unicode preview)", Dock = DockStyle.Fill };
    private readonly TableLayoutPanel _outputPanel = new() { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
    private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _detection = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _copy = new() { Text = "Copy result", AutoSize = true };
    private readonly NotifyIcon _tray;
    private readonly LiveTypingService _liveTyping;
    private readonly TypingComposer _composer;
    private readonly string _settingsPath;
    private HashSet<string> _installedFonts = new(StringComparer.OrdinalIgnoreCase);
    private ConversionResult? _lastResult;
    private string? _clipboardFontHint;
    private bool _closing;
    private bool _hotkeyRegistered;
    private bool _typingHotkeyRegistered;
    private bool _loadingSettings;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    public MainForm()
    {
        var extraProfiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SinhalaFontBridge", "Profiles");
        _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SinhalaFontBridge", "settings.json");
        _engine = ConversionEngine.LoadDefault(AppContext.BaseDirectory, extraProfiles);
        _composer = new TypingComposer(_engine, Path.Combine(AppContext.BaseDirectory, "singlish_keys.json"));
        _liveTyping = new LiveTypingService(
            _composer,
            reason => { _typingState.Text = "Live typing: off"; SetStatus(reason); });
        Text = "Sinhala Font Bridge";
        MinimumSize = new Size(760, 690);
        Size = new Size(980, 830);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 249, 252);
        BuildLayout();
        LoadSettings();
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Sinhala Font Bridge",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        _tray.ContextMenuStrip.Items.Add("Open", null, (_, _) => ShowWindow());
        _tray.ContextMenuStrip.Items.Add("Stop live typing", null, (_, _) => _liveTyping.Stop("Live typing stopped."));
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => { _closing = true; Close(); });
        _tray.DoubleClick += (_, _) => ShowWindow();
        Shown += (_, _) =>
        {
            _hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt | ModShift, (uint)Keys.S);
            _typingHotkeyRegistered = RegisterHotKey(Handle, TypingHotkeyId, ModControl | ModAlt | ModShift, (uint)Keys.T);
            SetStatus(_hotkeyRegistered
                ? "Ready. Copy text in any app, then press Ctrl+Alt+Shift+S."
                : "Shortcut unavailable (another app may use it). Use Paste from clipboard.");
            if (!_typingHotkeyRegistered)
                SetStatus("Live typing shortcut unavailable (another app may use Ctrl+Alt+Shift+T).");
            if (_target.SelectedItem is Choice target && target.Id != ConversionEngine.UnicodeId)
            {
                var profile = _engine.Profiles.First(p => p.Id == target.Id);
                if (!profile.MatchesFont(_font.Text)) ChooseTargetFont();
                ConvertCurrent(false);
            }
        };
        FormClosing += (_, e) =>
        {
            if (!_closing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _tray.ShowBalloonTip(1500, "Sinhala Font Bridge", "Still running. Ctrl+Alt+Shift+S converts copied text.", ToolTipIcon.Info);
            }
        };
        FormClosed += (_, _) =>
        {
            _liveTyping.Dispose();
            SaveSettings();
            if (_hotkeyRegistered) UnregisterHotKey(Handle, HotkeyId);
            if (_typingHotkeyRegistered) UnregisterHotKey(Handle, TypingHotkeyId);
            _tray.Dispose();
        };
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
        {
            ReadClipboardAndConvert(true);
            return;
        }
        if (message.Msg == WmHotkey && message.WParam.ToInt32() == TypingHotkeyId)
        {
            ToggleLiveTyping();
            return;
        }
        base.WndProc(ref message);
    }

    private void BuildLayout()
    {
        var page = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 8 };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 57));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(page);
        page.Controls.Add(new Label { Text = "Sinhala Font Bridge", Font = new Font("Segoe UI", 21, FontStyle.Bold), ForeColor = Color.FromArgb(28, 52, 83), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        page.Controls.Add(new Label { Text = "Unicode fonts share one encoding. Legacy fonts need their own conversion profile.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(69, 83, 104) }, 0, 1);

        var selectors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 4, 0, 4) };
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        selectors.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        selectors.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        selectors.Controls.Add(new Label { Text = "Source encoding", Dock = DockStyle.Fill }, 0, 0);
        selectors.Controls.Add(new Label { Text = "Target encoding", Dock = DockStyle.Fill }, 1, 0);
        selectors.Controls.Add(new Label { Text = "Target font for preview and rich copy", Dock = DockStyle.Fill }, 2, 0);
        selectors.Controls.Add(_source, 0, 1);
        selectors.Controls.Add(_target, 1, 1);
        selectors.Controls.Add(_font, 2, 1);
        page.Controls.Add(selectors, 0, 2);

        _source.Items.Add(new Choice("Auto detect", "auto"));
        _source.Items.Add(new Choice("Unicode Sinhala", ConversionEngine.UnicodeId));
        _source.Items.Add(new Choice("Wijesekara keys", WijesekaraId));
        _source.Items.Add(new Choice("Singlish sounds", SinglishId));
        _target.Items.Add(new Choice("Unicode Sinhala", ConversionEngine.UnicodeId));
        foreach (var profile in _engine.Profiles)
        {
            var choice = new Choice(profile.FontName + " (legacy)", profile.Id);
            _source.Items.Add(choice);
            if (profile.CanEncode) _target.Items.Add(choice);
        }
        _source.SelectedIndex = 0;
        _target.SelectedIndex = 0;
        _copy.Enabled = false;
        using var installed = new InstalledFontCollection();
        _installedFonts = installed.Families.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _font.Items.AddRange(installed.Families.Select(x => (object)x.Name).OrderBy(x => x).ToArray());
        _font.Text = installed.Families.Any(x => x.Name.Equals("Iskoola Pota", StringComparison.OrdinalIgnoreCase)) ? "Iskoola Pota" : "Nirmala UI";
        _font.TextChanged += (_, _) => { _liveTyping.Stop(); _typingState.Text = "Live typing: off"; UpdatePreviewFont(); ConvertCurrent(false); };
        _font.Leave += (_, _) => SaveSettings();

        var inputPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        inputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        inputPanel.Controls.Add(new Label { Text = "Input text", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        inputPanel.Controls.Add(_input, 0, 1);
        page.Controls.Add(inputPanel, 0, 3);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
        var paste = new Button { Text = "Paste from clipboard", AutoSize = true };
        var convert = new Button { Text = "Convert", AutoSize = true };
        var import = new Button { Text = "Import mapping profile", AutoSize = true };
        actions.Controls.AddRange([paste, convert, _copy, import]);
        page.Controls.Add(actions, 0, 4);
        paste.Click += (_, _) => ReadClipboardAndConvert(false);
        convert.Click += (_, _) => ConvertCurrent(false);
        _copy.Click += (_, _) => CopyResult();
        import.Click += (_, _) => ImportProfile();
        _input.TextChanged += (_, _) => { _clipboardFontHint = null; ConvertCurrent(false); };
        _source.SelectedIndexChanged += (_, _) => { ConvertCurrent(false); SaveSettings(); };
        _target.SelectedIndexChanged += (_, _) => { _liveTyping.Stop(); _typingState.Text = "Live typing: off"; ChooseTargetFont(); ConvertCurrent(false); SaveSettings(); };

        var typingPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
        _typingStyle.Items.AddRange(["Wijesekara keys", "Singlish sounds"]);
        _typingStyle.SelectedIndex = 0;
        _typingStyle.SelectedIndexChanged += (_, _) => { _liveTyping.Stop(); _typingState.Text = "Live typing: off"; SaveSettings(); };
        typingPanel.Controls.Add(new Label { Text = "Typing style", AutoSize = true, Padding = new Padding(0, 7, 7, 0) });
        typingPanel.Controls.Add(_typingStyle);
        typingPanel.Controls.Add(_typingState);
        typingPanel.Controls.Add(new Label { Text = "Focus another app; Ctrl+Alt+Shift+T toggles live typing", AutoSize = true, Padding = new Padding(14, 7, 0, 0) });
        page.Controls.Add(typingPanel, 0, 5);

        _outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        _outputPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _outputPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        _resultCaption.Font = new Font(Font, FontStyle.Bold);
        _unicodeCaption.Font = new Font(Font, FontStyle.Bold);
        _outputPanel.Controls.Add(_resultCaption, 0, 0);
        _outputPanel.Controls.Add(_output, 0, 1);
        _outputPanel.Controls.Add(_unicodeCaption, 0, 2);
        _outputPanel.Controls.Add(_unicodeCheck, 0, 3);
        page.Controls.Add(_outputPanel, 0, 6);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        footer.Controls.Add(_detection, 0, 0);
        footer.Controls.Add(_status, 0, 1);
        page.Controls.Add(footer, 0, 7);
    }

    private void ReadClipboardAndConvert(bool fromShortcut)
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                SetStatus("Clipboard has no text. Copy text in the other app first.");
                if (fromShortcut) ShowWindow();
                return;
            }
            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            _clipboardFontHint = Clipboard.ContainsText(TextDataFormat.Rtf) ? Clipboard.GetText(TextDataFormat.Rtf) : null;
            _input.Text = text;
            // TextChanged intentionally clears hints for manual editing.
            _clipboardFontHint = Clipboard.ContainsText(TextDataFormat.Rtf) ? Clipboard.GetText(TextDataFormat.Rtf) : null;
            ConvertCurrent(fromShortcut);
        }
        catch (ExternalException)
        {
            SetStatus("Clipboard is busy. Copy again and retry.");
            ShowWindow();
        }
    }

    private void ConvertCurrent(bool fromShortcut)
    {
        if (_source.SelectedItem is not Choice source || _target.SelectedItem is not Choice target) return;
        string effectiveInput = source.Id switch
        {
            SinglishId => _composer.Compose(_input.Text, TypingStyle.Singlish),
            WijesekaraId => _composer.Compose(_input.Text, TypingStyle.Wijesekara),
            _ => _input.Text
        };
        var detection = _engine.Detect(effectiveInput, _clipboardFontHint);
        _detection.Text = source.Id == "auto"
            ? "Detected: " + detection.SourceId + " · " + detection.Explanation
            : "Source selected: " + source.Name + " (manual).";
        _lastResult = _engine.Convert(effectiveInput,
            source.Id is SinglishId or WijesekaraId ? ConversionEngine.UnicodeId : source.Id,
            target.Id, _clipboardFontHint);
        _output.Text = _lastResult.Text;
        _unicodeCheck.Text = _lastResult.UnicodeText;
        bool legacyTarget = target.Id != ConversionEngine.UnicodeId;
        _outputPanel.RowStyles[2].Height = legacyTarget ? 26 : 0;
        _outputPanel.RowStyles[3].Height = legacyTarget ? 78 : 0;
        _unicodeCaption.Visible = legacyTarget;
        _unicodeCheck.Visible = legacyTarget;
        _resultCaption.Text = _lastResult.SourceId == "unknown"
            ? "No conversion yet — choose the source encoding"
            : legacyTarget && !_installedFonts.Contains(_font.Text)
                ? $"Legacy codes for {target.Name} — select this font in the target app"
                : legacyTarget ? $"Result in {target.Name}" : "Result (Unicode Sinhala)";
        UpdatePreviewFont();
        var profile = _engine.Profiles.FirstOrDefault(p => p.Id == target.Id);
        bool compatibleFont = profile is null || profile.MatchesFont(_font.Text);
        _copy.Enabled = _lastResult.CanCopy && _input.TextLength > 0 && compatibleFont;
        if (_lastResult.Warnings.Count > 0) SetStatus(string.Join("  ", _lastResult.Warnings));
        else if (!compatibleFont)
            SetStatus("Target font does not match this legacy encoding. Choose a listed family font.");
        else if (target.Id == ConversionEngine.UnicodeId && !_installedFonts.Contains(_font.Text))
            SetStatus("Selected Unicode font is not installed. Choose an installed Sinhala font for preview and Photoshop.");
        else if (target.Id != ConversionEngine.UnicodeId && !_installedFonts.Contains(_font.Text))
            SetStatus("This PC cannot preview that legacy font. The Unicode reading below lets you check the conversion; select the legacy font in your target app.");
        else if (_input.TextLength > 0) SetStatus("Ready. Review the result, then copy or use the shortcut.");
        if (fromShortcut)
        {
            bool sourceIsCertain = source.Id != "auto" || detection.IsCertain;
            if (_copy.Enabled && _lastResult.Warnings.Count == 0 && sourceIsCertain && _installedFonts.Contains(_font.Text))
            {
                CopyResult();
                _tray.ShowBalloonTip(1500, "Converted", "Result copied. Paste it in the target app.", ToolTipIcon.Info);
            }
            else
            {
                if (!sourceIsCertain) SetStatus("Source font is only a hint. Confirm it in the preview, then copy the result.");
                ShowWindow();
            }
        }
    }

    private void CopyResult()
    {
        if (_lastResult is not { CanCopy: true } || !_copy.Enabled) return;
        try
        {
            string fontName = _font.Text;
            if (!_installedFonts.Contains(fontName))
            {
                Clipboard.SetText(_lastResult.Text, TextDataFormat.UnicodeText);
                SetStatus("Copied character codes. Select the matching font in Photoshop before pasting.");
                return;
            }
            using var rich = new RichTextBox { Text = _lastResult.Text };
            rich.SelectAll();
            rich.SelectionFont = new Font(fontName, 12);
            rich.Copy(); // Plain Unicode text and RTF font hint for apps that accept rich paste.
            SetStatus("Copied. In Photoshop, choose the same target font on the text layer before pasting.");
        }
        catch (Exception e) when (e is ExternalException or ArgumentException)
        {
            SetStatus("Could not copy: " + e.Message);
        }
    }

    private void UpdatePreviewFont()
    {
        if (_target.SelectedItem is not Choice target) return;
        string name = _font.Text;
        if (!_installedFonts.Contains(name)) name = target.Id == ConversionEngine.UnicodeId ? "Nirmala UI" : "Consolas";
        _output.Font = new Font(name, 12);
    }

    private void ChooseTargetFont()
    {
        if (_target.SelectedItem is not Choice target || target.Id == ConversionEngine.UnicodeId) return;
        var profile = _engine.Profiles.First(p => p.Id == target.Id);
        _font.Text = _installedFonts.FirstOrDefault(profile.MatchesFont) ?? profile.FontName;
    }

    private void ToggleLiveTyping()
    {
        if (_liveTyping.Active)
        {
            _liveTyping.Stop("Live typing stopped.");
            return;
        }
        if (_target.SelectedItem is not Choice target || target.Id == ConversionEngine.UnicodeId)
        {
            SetStatus("Choose a supported legacy target before starting live typing.");
            ShowWindow();
            return;
        }
        var profile = _engine.Profiles.First(p => p.Id == target.Id);
        if (!profile.CanEncode || !profile.MatchesFont(_font.Text))
        {
            SetStatus("Select a target font name that matches the legacy encoding before starting live typing.");
            ShowWindow();
            return;
        }
        var style = _typingStyle.SelectedIndex == 1 ? TypingStyle.Singlish : TypingStyle.Wijesekara;
        if (!_liveTyping.Start(LiveTypingService.ForegroundWindow(), profile, style, out string error))
        {
            SetStatus(error);
            ShowWindow();
            return;
        }
        _typingState.Text = $"Live typing: on ({profile.FontName})";
        SetStatus("Live typing is on for this window. Esc or Ctrl+Alt+Shift+T stops it.");
        _tray.ShowBalloonTip(1500, "Sinhala live typing", $"{profile.FontName} · {style}", ToolTipIcon.Info);
    }

    private void ImportProfile()
    {
        using var picker = new OpenFileDialog { Filter = "JSON mapping profiles (*.json)|*.json", Title = "Choose a mapping profile" };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var profile = LegacyProfile.Load(picker.FileName);
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SinhalaFontBridge", "Profiles");
            Directory.CreateDirectory(folder);
            File.Copy(picker.FileName, Path.Combine(folder, Path.GetFileName(picker.FileName)), true);
            MessageBox.Show(this, $"Imported {profile.FontName}. Restart the app to load the new profile.", "Mapping imported");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            MessageBox.Show(this, e.Message, "Could not import mapping");
        }
    }

    internal void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void SetStatus(string message) => _status.Text = message;

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath));
            if (settings is null) return;
            _loadingSettings = true;
            if (_source.Items.Cast<Choice>().FirstOrDefault(x => x.Id == settings.SourceId) is { } source) _source.SelectedItem = source;
            if (_target.Items.Cast<Choice>().FirstOrDefault(x => x.Id == settings.TargetId) is { } target) _target.SelectedItem = target;
            if (!string.IsNullOrWhiteSpace(settings.UnicodeFont)) _font.Text = settings.UnicodeFont;
            if (settings.TypingStyle == "Singlish") _typingStyle.SelectedIndex = 1;
            if (_target.SelectedItem is Choice targetChoice && targetChoice.Id != ConversionEngine.UnicodeId &&
                !_engine.Profiles.First(p => p.Id == targetChoice.Id).MatchesFont(_font.Text))
                ChooseTargetFont();
            ConvertCurrent(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        finally { _loadingSettings = false; }
    }

    private void SaveSettings()
    {
        if (_loadingSettings || _source.SelectedItem is not Choice source || _target.SelectedItem is not Choice target) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new AppSettings(source.Id, target.Id, _font.Text,
                _typingStyle.SelectedIndex == 1 ? "Singlish" : "Wijesekara")));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private sealed record Choice(string Name, string Id)
    {
        public override string ToString() => Name;
    }
    private sealed record AppSettings(string SourceId, string TargetId, string UnicodeFont, string? TypingStyle = null);
}
