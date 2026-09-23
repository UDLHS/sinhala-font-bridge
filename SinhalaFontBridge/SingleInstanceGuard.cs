using System.Security.Principal;

namespace SinhalaFontBridge;

/// <summary>One app per Windows user/session, independent of its install folder.</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private bool _disposed;

    public bool IsOwner { get; }

    public SingleInstanceGuard(string applicationId = "SinhalaFontBridge.Desktop")
    {
        using var identity = WindowsIdentity.GetCurrent();
        var name = $@"Local\{applicationId}.{identity.User!.Value}";
        // Create the event first: a repeat launch during startup must not lose its request.
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        _mutex = new Mutex(false, name + ".Owner");
        try
        {
            IsOwner = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Windows grants ownership if the previous process crashed.
            IsOwner = true;
        }
    }

    public void RequestActivation() => _activation.Set();

    public bool TakeActivationRequest() => _activation.WaitOne(0);

    // Dispose on the same thread that acquired the mutex, after all app cleanup.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsOwner) _mutex.ReleaseMutex();
        _mutex.Dispose();
        _activation.Dispose();
    }
}
