using BigFatFile.Abstractions.Licensing;
using BigFatFile.Abstractions.Modules;

namespace BigFatFile.Origin.Modules;

internal static class ModuleLoader
{
    /// <summary>
    /// Finds modules in the modules folder: one subfolder per module, holding
    /// <c>&lt;folder name&gt;.dll</c> and its dependencies.
    /// </summary>
    public static IReadOnlyList<LoadedModule> Load(string modulesPath)
    {
        if (!Directory.Exists(modulesPath))
        {
            return [];
        }

        var coreVersion = typeof(IBffModule).Assembly.GetName().Version!;
        var modules = new List<LoadedModule>();
        foreach (var folder in Directory.GetDirectories(modulesPath).Order(StringComparer.Ordinal))
        {
            var mainAssembly = Path.Combine(folder, Path.GetFileName(folder) + ".dll");
            if (!File.Exists(mainAssembly))
            {
                throw new InvalidOperationException($"The module folder {folder} has no {Path.GetFileName(mainAssembly)}.");
            }

            var assembly = new ModuleLoadContext(mainAssembly).LoadFromAssemblyPath(mainAssembly);
            var types = assembly.GetExportedTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IBffModule).IsAssignableFrom(type))
                .ToList();
            if (types.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{mainAssembly} has {types.Count} public classes implementing {nameof(IBffModule)}; a module has exactly one.");
            }

            var module = (IBffModule)Activator.CreateInstance(types[0])!;
            var required = module.RequiredCoreVersion;
            if (required.Major != coreVersion.Major || required.Minor > coreVersion.Minor)
            {
                throw new InvalidOperationException(
                    $"The module {module.Name} needs core {required.ToString(2)}, and this core is {coreVersion.ToString(2)}.");
            }

            modules.Add(new LoadedModule(module, assembly, mainAssembly));
        }

        var duplicate = modules.GroupBy(m => m.Module.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Two modules are named {duplicate.Key}.");
        }

        return modules;
    }

    /// <summary>
    /// Lets each module register its services.
    /// Startup fails when more than one module registers an <see cref="IPlanPolicy"/>.
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, IReadOnlyList<LoadedModule> modules, BffModuleContext context)
    {
        var withPlanPolicy = new List<LoadedModule>();
        foreach (var loaded in modules)
        {
            var before = PlanPolicies(services);
            loaded.Module.ConfigureServices(services, context);
            if (PlanPolicies(services).Except(before).Any())
            {
                withPlanPolicy.Add(loaded);
            }
        }

        if (withPlanPolicy.Count > 1)
        {
            throw new InvalidOperationException(
                $"Only one module may register a plan policy ({nameof(IPlanPolicy)}), and these modules each register one: " +
                $"{string.Join(", ", withPlanPolicy.Select(loaded => $"{loaded.Module.Name} ({loaded.Path})"))}. " +
                $"Keep one of them in the modules folder, and remove the others.");
        }
    }

    // A keyed registration is left out, because it does not replace the core's policy.
    private static HashSet<ServiceDescriptor> PlanPolicies(IServiceCollection services) =>
        [.. services.Where(service => service.ServiceType == typeof(IPlanPolicy) && service.ServiceKey is null)];
}
