using HospitalityOS.Ports.Bindings;
using Microsoft.Extensions.Configuration;

namespace HospitalityOS.Bindings.Configuration;

/// <summary>
/// All bindings configured for one port:
/// <code>
/// "Bindings": {
///   "&lt;PortKey&gt;": {
///     "Default":    { "Adapter": "...", "Settings": { ... } },
///     "Operations": { "&lt;operation&gt;": { "Adapter": "...", ... } },
///     "Tenants":    { "&lt;tenant&gt;": { "Default": { ... }, "Operations": { ... } } }
///   }
/// }
/// </code>
/// </summary>
internal sealed class PortBindingConfiguration
{
    public const string DefaultKey = "Default";
    public const string OperationsKey = "Operations";
    public const string TenantsKey = "Tenants";

    private PortBindingConfiguration(
        string path,
        AdapterBinding? defaultBinding,
        IReadOnlyDictionary<string, AdapterBinding> operations,
        IReadOnlyDictionary<string, TenantBindingConfiguration> tenants)
    {
        Path = path;
        DefaultBinding = defaultBinding;
        Operations = operations;
        Tenants = tenants;
    }

    public string Path { get; }

    public AdapterBinding? DefaultBinding { get; }

    public IReadOnlyDictionary<string, AdapterBinding> Operations { get; }

    public IReadOnlyDictionary<string, TenantBindingConfiguration> Tenants { get; }

    public static PortBindingConfiguration FromSection(IConfigurationSection section)
    {
        Dictionary<string, TenantBindingConfiguration> tenants = section
            .GetSection(TenantsKey)
            .GetChildren()
            .ToDictionary(
                tenant => tenant.Key,
                tenant => new TenantBindingConfiguration(
                    AdapterBinding.FromSection(tenant.GetSection(DefaultKey)),
                    ReadOperations(tenant)),
                StringComparer.OrdinalIgnoreCase);

        return new PortBindingConfiguration(
            section.Path,
            AdapterBinding.FromSection(section.GetSection(DefaultKey)),
            ReadOperations(section),
            tenants);
    }

    /// <summary>
    /// Selects the most specific binding: tenant + operation, tenant default, operation, port default.
    /// </summary>
    public AdapterBinding? Select(BindingContext context)
    {
        if (context.Tenant is not null && Tenants.TryGetValue(context.Tenant, out TenantBindingConfiguration? tenant))
        {
            if (context.Operation is not null && tenant.Operations.TryGetValue(context.Operation, out AdapterBinding? tenantOperation))
            {
                return tenantOperation;
            }

            if (tenant.DefaultBinding is not null)
            {
                return tenant.DefaultBinding;
            }
        }

        if (context.Operation is not null && Operations.TryGetValue(context.Operation, out AdapterBinding? operation))
        {
            return operation;
        }

        return DefaultBinding;
    }

    /// <summary>
    /// Every binding configured for the port, for validation and health checks.
    /// </summary>
    public IEnumerable<AdapterBinding> All()
    {
        if (DefaultBinding is not null)
        {
            yield return DefaultBinding;
        }

        foreach (AdapterBinding operation in Operations.Values)
        {
            yield return operation;
        }

        foreach (TenantBindingConfiguration tenant in Tenants.Values)
        {
            if (tenant.DefaultBinding is not null)
            {
                yield return tenant.DefaultBinding;
            }

            foreach (AdapterBinding operation in tenant.Operations.Values)
            {
                yield return operation;
            }
        }
    }

    private static Dictionary<string, AdapterBinding> ReadOperations(IConfigurationSection parent)
    {
        Dictionary<string, AdapterBinding> operations = new(StringComparer.OrdinalIgnoreCase);

        foreach (IConfigurationSection operation in parent.GetSection(OperationsKey).GetChildren())
        {
            AdapterBinding? binding = AdapterBinding.FromSection(operation);

            if (binding is not null)
            {
                operations[operation.Key] = binding;
            }
        }

        return operations;
    }
}
