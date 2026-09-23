using System.Diagnostics;
using SinhalaFontBridge;

internal static class SingleInstanceChecks
{
    public static void Child(string name)
    {
        using var guard = new SingleInstanceGuard(name);
        if (!guard.IsOwner)
        {
            guard.RequestActivation();
            Console.WriteLine("duplicate");
            return;
        }
        Console.WriteLine("owner");
        Console.ReadLine();
    }

    public static void Run()
    {
        var name = "SinhalaFontBridge.Checks." + Guid.NewGuid().ToString("N");
        using (var owner = new SingleInstanceGuard(name))
        {
            Check(owner.IsOwner, "first process owns lock");
            using var duplicate = Start(name);
            Check(Read(duplicate) == "duplicate", "second process must exit before creating UI");
            Finish(duplicate);
            Check(owner.TakeActivationRequest(), "request during startup survives until UI is ready");
            Check(!owner.TakeActivationRequest(), "activation is consumed once");
            var burst = Enumerable.Range(0, 6).Select(_ => Start(name)).ToArray();
            try
            {
                foreach (var process in burst)
                {
                    Check(Read(process) == "duplicate", "rapid repeat launches cannot own lock");
                    Finish(process);
                }
                Check(owner.TakeActivationRequest(), "rapid launches notify owner");
            }
            finally
            {
                foreach (var process in burst) Stop(process);
            }
        }
        using (var restarted = Start(name))
        {
            Check(Read(restarted) == "owner", "launch after normal exit succeeds");
            restarted.StandardInput.WriteLine("exit");
            Finish(restarted);
        }
        using (var crashed = Start(name))
        {
            try
            {
                Check(Read(crashed) == "owner", "crash test owns lock");
                using var observer = new SingleInstanceGuard(name);
                Check(!observer.IsOwner, "running owner excludes observer");
                crashed.Kill();
                Check(crashed.WaitForExit(5000), "crash test process exits");
                using var recovered = new SingleInstanceGuard(name);
                Check(recovered.IsOwner, "abandoned mutex can be recovered after crash");
            }
            finally { Stop(crashed); }
        }
        Console.WriteLine("PASS: single instance across processes, startup activation, rapid launches, normal restart, and crash recovery.");
    }

    private static Process Start(string name)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!)
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, CreateNoWindow = true
        };
        info.ArgumentList.Add("--instance-child");
        info.ArgumentList.Add(name);
        return Process.Start(info)!;
    }

    private static string? Read(Process process) => process.StandardOutput.ReadLineAsync()
        .WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

    private static void Finish(Process process)
    {
        Check(process.WaitForExit(5000), "child exits promptly");
        Check(process.ExitCode == 0, "child exits successfully");
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited) { process.Kill(); process.WaitForExit(5000); }
        process.Dispose();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
