using System.Reflection;
using System.Runtime.Loader;

namespace BigFatFile.Server.Modules;

/// <summary>
/// Loads one module and its own dependencies.
/// The assemblies the core and the module pass types through come from the core, so each such type exists only once.
/// </summary>
internal sealed class ModuleLoadContext(string mainAssemblyPath)
    : AssemblyLoadContext(Path.GetFileNameWithoutExtension(mainAssemblyPath))
{
    private static readonly HashSet<string> Shared = new(StringComparer.Ordinal)
    {
        "BigFatFile.Abstractions",
        "BigFatFile.Contracts",
    };

    private readonly AssemblyDependencyResolver resolver = new(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Returning null hands the request to the default context,
        // which also covers the ASP.NET Core shared framework.
        if (assemblyName.Name is null || Shared.Contains(assemblyName.Name))
        {
            return null;
        }

        var path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
