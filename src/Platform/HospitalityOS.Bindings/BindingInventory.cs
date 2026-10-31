using HospitalityOS.Bindings.Configuration;
using HospitalityOS.Bindings.Inference;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalityOS.Bindings;

internal sealed class BindingInventory(IServiceProvider services, IEnumerable<PortRegistration> registrations) : IBindingInventory
{
    public IReadOnlyList<BoundPort> Ports(string tenant)
    {
        List<BoundPort> ports = [];

        foreach (PortRegistration registration in registrations.OrderBy(registration => registration.Key, StringComparer.Ordinal))
        {
            PortBindingConfiguration configuration = registration.ConfigurationIn(services);

            if (configuration.DefaultBinding is { } binding)
            {
                ports.Add(new BoundPort(registration.Key, "Default", binding.Adapter));
            }

            ports.AddRange(configuration.Operations.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new BoundPort(registration.Key, $"Operation:{pair.Key}", pair.Value.Adapter)));

            if (configuration.Tenants.TryGetValue(tenant, out TenantBindingConfiguration? own))
            {
                if (own.DefaultBinding is { } tenantDefault)
                {
                    ports.Add(new BoundPort(registration.Key, "Tenant", tenantDefault.Adapter));
                }

                ports.AddRange(own.Operations.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new BoundPort(registration.Key, $"Tenant operation:{pair.Key}", pair.Value.Adapter)));
            }
        }

        return ports;
    }

    public IReadOnlyList<BoundInferenceTask> InferenceTasks(string tenant)
    {
        // Hosts without inference declare no tasks.
        if (services.GetService<InferenceCatalog>() is not { } catalog)
        {
            return [];
        }

        InferenceConfiguration configuration = catalog.Configuration;

        return [.. catalog.Tasks.OrderBy(task => task.Name, StringComparer.Ordinal).Select(task =>
        {
            string capability = task.Port.Name.StartsWith('I') ? task.Port.Name[1..] : task.Port.Name;

            if (configuration.Select(task.Name, tenant) is not { } route)
            {
                return new BoundInferenceTask(task.Name, capability, "", "", "", []);
            }

            string adapter = configuration.Providers.GetValueOrDefault(route.Primary.Provider)?.Adapter ?? "";

            return new BoundInferenceTask(
                task.Name,
                capability,
                route.Primary.Provider,
                adapter,
                route.Primary.Model,
                [.. route.Fallbacks.Select(fallback => $"{fallback.Provider}/{fallback.Model}")]);
        })];
    }
}
