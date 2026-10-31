using System.Reflection;
using BigFatFile.Abstractions.Modules;

namespace BigFatFile.Server.Modules;

internal sealed record LoadedModule(IBffModule Module, Assembly Assembly, string Path);
