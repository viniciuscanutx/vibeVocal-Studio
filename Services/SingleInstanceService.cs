using System.Security.Cryptography;
using System.Text;

namespace VibeVocalStudio.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private readonly RegisteredWaitHandle? listener;
    private bool disposed;

    public bool IsPrimary { get; }

    public SingleInstanceService(string identity, Action activate, bool activateExisting)
    {
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant())))[..24];
        var name = @"Local\VibeVocalStudio-" + suffix;
        mutex = new Mutex(true, name, out var created);
        IsPrimary = created;
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-Show");
        if (IsPrimary)
            listener = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => activate(), null, Timeout.Infinite, false);
        else if (activateExisting)
            activation.Set();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        listener?.Unregister(null);
        activation.Dispose();
        if (IsPrimary)
            mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
