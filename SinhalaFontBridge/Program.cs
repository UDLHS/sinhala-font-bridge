namespace SinhalaFontBridge;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Guard before constructing any window, tray icon, settings, or keyboard hooks.
        using var instance = new SingleInstanceGuard();
        if (!instance.IsOwner)
        {
            instance.RequestActivation();
            return;
        }
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--capture")
        {
            using var form = new MainForm();
            form.Show();
            Application.DoEvents();
            using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
            return;
        }
        using var mainForm = new MainForm();
        using var activationTimer = new System.Windows.Forms.Timer { Interval = 200 };
        activationTimer.Tick += (_, _) =>
        {
            if (instance.TakeActivationRequest()) mainForm.ShowWindow();
        };
        activationTimer.Start();
        Application.Run(mainForm);
    }
}
