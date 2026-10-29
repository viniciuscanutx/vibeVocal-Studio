namespace VibeVocalStudio.Models;

public sealed record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record BufferOption(int Milliseconds, string Name);
