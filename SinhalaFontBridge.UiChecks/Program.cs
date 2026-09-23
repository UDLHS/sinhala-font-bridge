using System.Reflection;
using SinhalaFontBridge;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--instance-child")
        {
            SingleInstanceChecks.Child(args[1]);
            return;
        }
        SingleInstanceChecks.Run();
        ApplicationConfiguration.Initialize();
        using var form = new MainForm();
        // Exercise the actual controls without changing the user's saved choices.
        typeof(MainForm).GetField("_loadingSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(form, true);
        form.ShowInTaskbar = false;
        form.Opacity = 0;
        form.Show();
        Application.DoEvents();
        var source = Field<ComboBox>(form, "_source");
        var target = Field<ComboBox>(form, "_target");
        var input = Field<TextBox>(form, "_input");
        var output = Field<TextBox>(form, "_output");
        var reading = Field<TextBox>(form, "_unicodeCheck");
        var copy = Field<Button>(form, "_copy");
        Select(target, "fm_abhaya");
        Field<ComboBox>(form, "_font").Text = "FMAbhaya";
        Select(source, "auto");
        input.Text = "oq";
        Check(output.Text == "" && reading.Text == "" && !copy.Enabled, "Unknown source must show no converted result");
        Select(source, "wijesekara");
        Check(output.Text == "ÿ" && reading.Text == "දු" && copy.Enabled, "Wijesekara panel must produce the special FM glyph");
        Select(source, "singlish");
        input.Text = "dhu";
        Check(output.Text == "ÿ" && reading.Text == "දු" && copy.Enabled, "Singlish panel must produce the special FM glyph");
        Select(source, "wijesekara");
        input.Text = "oq";
        if (args.Length == 1)
        {
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.GetFullPath(args[0]), System.Drawing.Imaging.ImageFormat.Png);
        }
        form.Hide();
        form.WindowState = FormWindowState.Minimized;
        typeof(MainForm).GetMethod("ShowWindow", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null);
        Application.DoEvents();
        Check(form.Visible && form.WindowState == FormWindowState.Normal, "Repeat launch restores the hidden/minimized window");
        Console.WriteLine("PASS: existing form restores from hidden/minimized state.");
        Field<NotifyIcon>(form, "_tray").Dispose();
        Console.WriteLine("PASS: actual converter controls — unknown source, Wijesekara oq, Singlish dhu, Unicode preview, and Copy state.");
    }

    private static T Field<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;

    private static void Select(ComboBox box, string id) => box.SelectedItem = box.Items.Cast<object>()
        .Single(item => (string)item.GetType().GetProperty("Id")!.GetValue(item)! == id);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
