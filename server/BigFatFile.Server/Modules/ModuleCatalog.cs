namespace BigFatFile.Server.Modules;

/// <summary>The modules loaded at startup, in load order.</summary>
internal sealed class ModuleCatalog(IReadOnlyList<LoadedModule> modules)
{
    public IReadOnlyList<LoadedModule> Modules { get; } = modules;
}
